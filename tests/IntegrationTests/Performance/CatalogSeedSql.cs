namespace IntegrationTests.Performance;

/// <summary>
/// Builds the set-based T-SQL that seeds the representative catalog dataset. The script is a
/// pure function of the <see cref="CatalogDatasetSpec"/> (its three counts are passed as SQL
/// parameters, never interpolated), so it is reviewable and testable without a database.
/// </summary>
/// <remarks>
/// The script targets the schema produced by the committed EF migrations and inserts directly with
/// set-based statements: seeding 5,000 variants and 50,000 pieces through the API or the change
/// tracker would dominate the run without telling us anything about request latency. Every seeded
/// row is recognizable by the <see cref="CatalogDatasetSpec.SkuPrefix"/> /
/// <see cref="CatalogDatasetSpec.PieceCodePrefix"/> code prefix. Variants alternate
/// Quantity/Serialized; only serialized variants own pieces.
/// </remarks>
public static class CatalogSeedSql
{
    /// <summary>The SQL parameter holding the product count.</summary>
    public const string ProductsParameter = "@products";

    /// <summary>The SQL parameter holding the variants-per-product count.</summary>
    public const string VariantsPerProductParameter = "@vpp";

    /// <summary>The SQL parameter holding the pieces-per-serialized-variant count.</summary>
    public const string PiecesPerVariantParameter = "@ppv";

    /// <summary>Gets the seed script. It expects the three count parameters defined by this type.</summary>
    public static string Script { get; } =
        """
        SET NOCOUNT ON;

        DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();
        DECLARE @variantCount int = @products * @vpp;
        DECLARE @maxN int = (SELECT MAX(v) FROM (VALUES (@products), (@variantCount), (@ppv)) AS t(v));

        CREATE TABLE #Numbers (N int NOT NULL PRIMARY KEY);
        INSERT INTO #Numbers (N)
        SELECT TOP (@maxN) ROW_NUMBER() OVER (ORDER BY (SELECT NULL))
        FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b;

        CREATE TABLE #Products (N int NOT NULL PRIMARY KEY, Id uniqueidentifier NOT NULL);
        INSERT INTO #Products (N, Id)
        SELECT N, NEWID() FROM #Numbers WHERE N <= @products;

        INSERT INTO Products (Id, Name, Category, PublicDescription, InternalNotes, PublicationState, ActiveFlag, CreatedAtUtc, UpdatedAtUtc)
        SELECT Id,
               CONCAT(N'Performance product ', N),
               CONCAT(N'Category ', N % 10),
               N'Seeded public description used for catalog performance verification.',
               N'Seeded internal note.',
               N'Draft', 1, @now, @now
        FROM #Products;

        CREATE TABLE #Variants (N int NOT NULL PRIMARY KEY, Id uniqueidentifier NOT NULL, IsSerialized bit NOT NULL);
        INSERT INTO #Variants (N, Id, IsSerialized)
        SELECT N, NEWID(), CASE WHEN N % 2 = 0 THEN 1 ELSE 0 END
        FROM #Numbers WHERE N <= @variantCount;

        INSERT INTO ProductVariants (Id, ProductId, Sku, Barcode, TrackingMode, UnitOfMeasure, Length, Width, Thickness, Diameter, DimensionUnit, Finish, RetailPrice, WholesalePrice, CasePack, CareProfileId, ActiveFlag, CreatedAtUtc, UpdatedAtUtc)
        SELECT v.Id,
               p.Id,
               CONCAT(N'PERF-', RIGHT(CONCAT(N'00000000', v.N), 8)),
               CONCAT(N'PB', RIGHT(CONCAT(N'0000000000', v.N), 10)),
               CASE WHEN v.IsSerialized = 1 THEN N'Serialized' ELSE N'Quantity' END,
               N'each',
               12.0 + (v.N % 20), 6.0, 0.75, NULL, N'in',
               N'Natural oil',
               25.00 + (v.N % 40),
               12.50 + (v.N % 20),
               CASE WHEN v.IsSerialized = 0 THEN 6 ELSE NULL END,
               NULL, 1, @now, @now
        FROM #Variants AS v
        INNER JOIN #Products AS p ON p.N = ((v.N - 1) / @vpp) + 1;

        INSERT INTO ProductPieces (Id, VariantId, PieceCode, Length, Width, Thickness, Diameter, DimensionUnit, Finish, Story, Status, ProductionDate, CareProfileVersionId, ProductionBatchLineId, PublicationState, ActiveFlag, CreatedAtUtc, UpdatedAtUtc)
        SELECT NEWID(),
               v.Id,
               CONCAT(N'PERF-P-', RIGHT(CONCAT(N'00000000', v.N), 8), N'-', RIGHT(CONCAT(N'000', n.N), 3)),
               12.5, 6.0, 0.75, NULL, N'in',
               N'Natural oil',
               N'Seeded maker story for catalog performance verification.',
               N'InStock',
               DATEADD(day, -n.N, @now),
               NULL, NULL, N'Draft', 1, @now, @now
        FROM #Variants AS v
        INNER JOIN #Numbers AS n ON n.N <= @ppv
        WHERE v.IsSerialized = 1;

        DROP TABLE #Variants;
        DROP TABLE #Products;
        DROP TABLE #Numbers;
        """;
}
