using IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Performance;

/// <summary>
/// Verifies the seed SQL against the real migrated schema using the small <see cref="CatalogDatasetSpec.Smoke"/>
/// shape, so a schema change that breaks the performance seed is caught in the default run rather
/// than at the first performance run. Uses its own isolated database (class fixture), never the
/// shared persistence database.
/// </summary>
public sealed class CatalogDatasetSeederTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public CatalogDatasetSeederTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task SeedAsync_SmokeSpec_InsertsExactCountsAndRefusesASecondSeedAsync()
    {
        CatalogDatasetSpec spec = CatalogDatasetSpec.Smoke;

        await CatalogDatasetSeeder.SeedAsync(_database.ConnectionString, spec, CancellationToken.None);

        await using PenguinPlankDbContext context = _database.CreateContext();
        Assert.Equal(spec.ProductCount, await context.Products.CountAsync());
        Assert.Equal(spec.VariantCount, await context.ProductVariants.CountAsync());
        Assert.Equal(spec.PieceCount, await context.ProductPieces.CountAsync());

        CatalogDatasetSample sample = await CatalogDatasetSeeder.LoadSampleAsync(_database.ConnectionString, CancellationToken.None);
        Assert.Equal(spec.ProductCount, sample.ProductIds.Count);
        Assert.Equal(spec.VariantCount, sample.VariantIds.Count);
        Assert.Equal(spec.SerializedVariantCount, sample.SerializedVariantIds.Count);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CatalogDatasetSeeder.SeedAsync(_database.ConnectionString, spec, CancellationToken.None));
    }
}
