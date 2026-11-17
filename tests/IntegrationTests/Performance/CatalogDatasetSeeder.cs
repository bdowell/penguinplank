using Microsoft.Data.SqlClient;

namespace IntegrationTests.Performance;

/// <summary>Identifiers of seeded rows the workload targets, in deterministic SKU order.</summary>
/// <param name="ProductIds">Every seeded product id.</param>
/// <param name="VariantIds">Every seeded variant id.</param>
/// <param name="SerializedVariantIds">The seeded variants that own pieces.</param>
public sealed record CatalogDatasetSample(
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid> VariantIds,
    IReadOnlyList<Guid> SerializedVariantIds);

/// <summary>
/// Seeds the representative catalog dataset into an <b>isolated test database that already has
/// the actual migrations applied</b>. It never creates or migrates a database itself and refuses a
/// database that already holds catalog rows, so it cannot be pointed at a populated (for example
/// production) database by accident.
/// </summary>
public static class CatalogDatasetSeeder
{
    private const int SeedTimeoutSeconds = 600;

    /// <summary>Seeds the dataset described by <paramref name="spec"/>.</summary>
    /// <param name="connectionString">The connection string of the isolated, migrated database.</param>
    /// <param name="spec">The dataset shape.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the rows are committed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the target database already contains catalog rows.</exception>
    public static async Task SeedAsync(string connectionString, CatalogDatasetSpec spec, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(spec);
        spec.Validate();

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (SqlCommand guard = new("SELECT COUNT_BIG(*) FROM Products", connection))
        {
            long existing = (long)(await guard.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
            if (existing != 0)
            {
                throw new InvalidOperationException(
                    "Refusing to seed: the target database already contains products. The performance seed runs only against an empty, freshly migrated test database.");
            }
        }

        await using SqlCommand command = new(CatalogSeedSql.Script, connection)
        {
            CommandTimeout = SeedTimeoutSeconds,
        };
        command.Parameters.AddWithValue(CatalogSeedSql.ProductsParameter, spec.ProductCount);
        command.Parameters.AddWithValue(CatalogSeedSql.VariantsPerProductParameter, spec.VariantsPerProduct);
        command.Parameters.AddWithValue(CatalogSeedSql.PiecesPerVariantParameter, spec.PiecesPerSerializedVariant);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads back the seeded ids the workload needs to target existing records.</summary>
    /// <param name="connectionString">The connection string of the seeded database.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The sample.</returns>
    public static async Task<CatalogDatasetSample> LoadSampleAsync(string connectionString, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Guid> products = await ReadIdsAsync(
            connection, "SELECT Id FROM Products ORDER BY Name", cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Guid> variants = await ReadIdsAsync(
            connection, "SELECT Id FROM ProductVariants ORDER BY Sku", cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Guid> serialized = await ReadIdsAsync(
            connection, "SELECT Id FROM ProductVariants WHERE TrackingMode = N'Serialized' ORDER BY Sku", cancellationToken).ConfigureAwait(false);

        return new CatalogDatasetSample(products, variants, serialized);
    }

    private static async Task<IReadOnlyList<Guid>> ReadIdsAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        List<Guid> ids = [];
        await using SqlCommand command = new(sql, connection);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }
}
