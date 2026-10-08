using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests asserting that the committed EF Core migration produces the relational
/// constraints the Phase A schema promises, exercised against a real isolated SQL Server
/// database (coding-standards Â§7; requirements A2 5.1, 5.3, 5.6, 5.7).
/// </summary>
/// <remarks>
/// Each test arranges the minimal rows it needs through the shared
/// <see cref="SqlServerDatabaseFixture"/> and asserts observable persistence behavior â€” a unique
/// or filtered-unique index rejecting a duplicate, a restrict foreign key refusing a cascading
/// delete, a foreign key refusing an orphan child, the <c>rowversion</c> token advancing on
/// update, and the stored decimal precision/scale of the money and dimension columns. No test
/// mocks a <c>DbSet</c>/<c>IQueryable</c>, because that cannot prove relational correctness.
/// </remarks>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class SchemaConstraintTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public SchemaConstraintTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // -----------------------------------------------------------------------------------------
    // Unique SKU (requirement 5.1 / invariant 1).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_DuplicateVariantSku_ViolatesUniqueIndexAsync()
    {
        Product product = await InsertProductAsync();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            context.ProductVariants.Add(NewVariant(product.Id, sku: "SKU-UNIQUE-1"));
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext duplicateContext = _fixture.CreateContext();
        duplicateContext.ProductVariants.Add(NewVariant(product.Id, sku: "SKU-UNIQUE-1"));

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());

        Assert.True(IsUniqueConstraintViolation(exception));
    }

    // -----------------------------------------------------------------------------------------
    // Filtered-unique PieceCode (requirement 5.1 / invariant 1): duplicate non-null fails,
    // multiple NULLs allowed.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_DuplicatePieceCode_ViolatesFilteredUniqueIndexAsync()
    {
        Guid variantId = await InsertSerializedVariantAsync();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            context.ProductPieces.Add(NewPiece(variantId, pieceCode: "PC-DUP-1"));
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext duplicateContext = _fixture.CreateContext();
        duplicateContext.ProductPieces.Add(NewPiece(variantId, pieceCode: "PC-DUP-1"));

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());

        Assert.True(IsUniqueConstraintViolation(exception));
    }

    [Fact]
    public async Task SaveChangesAsync_MultipleNullPieceCodes_AreAllowedByFilteredIndexAsync()
    {
        Guid variantId = await InsertSerializedVariantAsync();

        await using PenguinPlankDbContext context = _fixture.CreateContext();
        context.ProductPieces.Add(NewPiece(variantId, pieceCode: null));
        context.ProductPieces.Add(NewPiece(variantId, pieceCode: null));

        // Two NULL piece codes must coexist: the unique index is filtered to NOT NULL.
        int written = await context.SaveChangesAsync();

        Assert.Equal(2, written);
    }

    // -----------------------------------------------------------------------------------------
    // Filtered-unique Barcode (invariant 1): duplicate non-null fails, multiple NULLs allowed.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_DuplicateBarcode_ViolatesFilteredUniqueIndexAsync()
    {
        Product product = await InsertProductAsync();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            ProductVariant first = NewVariant(product.Id, sku: "SKU-BC-A");
            first.Barcode = "BC-DUP-1";
            context.ProductVariants.Add(first);
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext duplicateContext = _fixture.CreateContext();
        ProductVariant second = NewVariant(product.Id, sku: "SKU-BC-B");
        second.Barcode = "BC-DUP-1";
        duplicateContext.ProductVariants.Add(second);

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());

        Assert.True(IsUniqueConstraintViolation(exception));
    }

    [Fact]
    public async Task SaveChangesAsync_MultipleNullBarcodes_AreAllowedByFilteredIndexAsync()
    {
        Product product = await InsertProductAsync();

        await using PenguinPlankDbContext context = _fixture.CreateContext();
        context.ProductVariants.Add(NewVariant(product.Id, sku: "SKU-NULLBC-A"));
        context.ProductVariants.Add(NewVariant(product.Id, sku: "SKU-NULLBC-B"));

        int written = await context.SaveChangesAsync();

        Assert.Equal(2, written);
    }

    // -----------------------------------------------------------------------------------------
    // No cascade-delete of history (requirement 5.7 / invariant 5). Deleting a Product that
    // still has a dependent ProductVariant must be restricted rather than cascading.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_DeleteProductWithVariant_IsRestrictedAsync()
    {
        Product product = await InsertProductAsync();

        await using (PenguinPlankDbContext seed = _fixture.CreateContext())
        {
            seed.ProductVariants.Add(NewVariant(product.Id, sku: "SKU-RESTRICT-1"));
            await seed.SaveChangesAsync();
        }

        await using PenguinPlankDbContext deleteContext = _fixture.CreateContext();
        Product tracked = await deleteContext.Products.SingleAsync(p => p.Id == product.Id);
        deleteContext.Products.Remove(tracked);

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());

        Assert.True(IsForeignKeyViolation(exception));

        // The dependent history row (the variant) must still exist â€” nothing cascaded.
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.True(await verify.ProductVariants.AnyAsync(v => v.ProductId == product.Id));
    }

    [Fact]
    public async Task SaveChangesAsync_DeleteConnectionWithInbox_IsRestrictedAsync()
    {
        Guid connectionId = await InsertConnectionAsync();

        await using (PenguinPlankDbContext seed = _fixture.CreateContext())
        {
            seed.IntegrationInbox.Add(NewInbox(connectionId, deliveryKey: "DK-RESTRICT-1"));
            await seed.SaveChangesAsync();
        }

        await using PenguinPlankDbContext deleteContext = _fixture.CreateContext();
        IntegrationConnection tracked =
            await deleteContext.IntegrationConnections.SingleAsync(c => c.Id == connectionId);
        deleteContext.IntegrationConnections.Remove(tracked);

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());

        Assert.True(IsForeignKeyViolation(exception));

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.True(await verify.IntegrationInbox.AnyAsync(i => i.ConnectionId == connectionId));
    }

    // -----------------------------------------------------------------------------------------
    // Foreign-key enforcement (requirement 5.3): a child referencing a non-existent parent fails.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_VariantWithNonExistentProduct_ViolatesForeignKeyAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        context.ProductVariants.Add(NewVariant(Guid.NewGuid(), sku: "SKU-ORPHAN-1"));

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.True(IsForeignKeyViolation(exception));
    }

    // -----------------------------------------------------------------------------------------
    // rowversion presence and advancement (requirement 5.6): a VersionedEntity gets a non-null
    // token on insert that changes on update.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_InsertAndUpdateProduct_RowVersionIsPresentAndChangesAsync()
    {
        Guid productId;
        byte[] afterInsert;

        await using (PenguinPlankDbContext insertContext = _fixture.CreateContext())
        {
            Product product = NewProduct();
            insertContext.Products.Add(product);
            await insertContext.SaveChangesAsync();

            productId = product.Id;
            afterInsert = product.RowVersion;
        }

        Assert.NotNull(afterInsert);
        Assert.NotEmpty(afterInsert);

        byte[] afterUpdate;
        await using (PenguinPlankDbContext updateContext = _fixture.CreateContext())
        {
            Product tracked = await updateContext.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "Renamed after insert";
            await updateContext.SaveChangesAsync();
            afterUpdate = tracked.RowVersion;
        }

        Assert.NotEmpty(afterUpdate);
        Assert.False(
            afterInsert.AsSpan().SequenceEqual(afterUpdate),
            "rowversion must advance on update so stale edits are detectable.");
    }

    // -----------------------------------------------------------------------------------------
    // Immutable append-only entity (CareProfileVersion): new care edits append new versions and
    // never mutate existing rows (requirement 2.3/2.5 reinforced at the row level).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_AppendingCareVersions_LeavesPriorVersionsUnchangedAsync()
    {
        Guid careProfileId = await InsertCareProfileAsync();

        Guid firstVersionId;
        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            CareProfileVersion first = new()
            {
                Id = Guid.NewGuid(),
                CareProfileId = careProfileId,
                VersionNumber = 1,
                Guidance = "Hand wash only.",
            };
            context.CareProfileVersions.Add(first);
            await context.SaveChangesAsync();
            firstVersionId = first.Id;
        }

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            CareProfileVersion second = new()
            {
                Id = Guid.NewGuid(),
                CareProfileId = careProfileId,
                VersionNumber = 2,
                Guidance = "Hand wash and re-oil monthly.",
            };
            context.CareProfileVersions.Add(second);
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        CareProfileVersion preserved =
            await verify.CareProfileVersions.SingleAsync(v => v.Id == firstVersionId);

        Assert.Equal(1, preserved.VersionNumber);
        Assert.Equal("Hand wash only.", preserved.Guidance);
        Assert.Equal(2, await verify.CareProfileVersions.CountAsync(v => v.CareProfileId == careProfileId));
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateCareVersionNumber_ViolatesUniqueIndexAsync()
    {
        Guid careProfileId = await InsertCareProfileAsync();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            context.CareProfileVersions.Add(new CareProfileVersion
            {
                Id = Guid.NewGuid(),
                CareProfileId = careProfileId,
                VersionNumber = 1,
                Guidance = "v1",
            });
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext duplicate = _fixture.CreateContext();
        duplicate.CareProfileVersions.Add(new CareProfileVersion
        {
            Id = Guid.NewGuid(),
            CareProfileId = careProfileId,
            VersionNumber = 1,
            Guidance = "v1-again",
        });

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());

        Assert.True(IsUniqueConstraintViolation(exception));
    }

    // -----------------------------------------------------------------------------------------
    // Decimal precision/scale (requirement 5.3): money is decimal(19,4), dimensions decimal(12,4).
    // Verified both from the catalog metadata and from an actual stored value's rounding.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ProductVariants", "RetailPrice", 19, 4)]
    [InlineData("ProductVariants", "WholesalePrice", 19, 4)]
    [InlineData("ProductVariants", "Length", 12, 4)]
    [InlineData("ProductVariants", "Width", 12, 4)]
    [InlineData("ProductVariants", "Diameter", 12, 4)]
    public async Task InformationSchema_DecimalColumn_HasExpectedPrecisionAndScaleAsync(
        string tableName,
        string columnName,
        int expectedPrecision,
        int expectedScale)
    {
        await using SqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using SqlCommand command = new(
            "SELECT NUMERIC_PRECISION, NUMERIC_SCALE " +
            "FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_NAME = @table AND COLUMN_NAME = @column",
            connection);
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@column", columnName);

        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Column {tableName}.{columnName} should exist.");

        // INFORMATION_SCHEMA.COLUMNS exposes NUMERIC_PRECISION as tinyint and NUMERIC_SCALE as
        // int; read both through Convert so neither column's SQL type is assumed.
        int precision = Convert.ToInt32(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
        int scale = Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expectedPrecision, precision);
        Assert.Equal(expectedScale, scale);
    }

    [Fact]
    public async Task SaveChangesAsync_MoneyWithMoreThanFourDecimals_IsRoundedToScaleFourAsync()
    {
        Product product = await InsertProductAsync();
        Guid variantId;

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            ProductVariant variant = NewVariant(product.Id, sku: "SKU-MONEY-1");
            // decimal(19,4): a 6-dp input is stored at scale 4 (rounded), never kept at 6 dp.
            variant.RetailPrice = 19.123456m;
            context.ProductVariants.Add(variant);
            await context.SaveChangesAsync();
            variantId = variant.Id;
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        ProductVariant stored = await verify.ProductVariants.SingleAsync(v => v.Id == variantId);

        Assert.Equal(19.1235m, stored.RetailPrice);
    }

    // -----------------------------------------------------------------------------------------
    // Arrange helpers â€” minimal, explicit fixtures (no shared seeded dataset, Â§7).
    // -----------------------------------------------------------------------------------------

    private async Task<Product> InsertProductAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        Product product = NewProduct();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private async Task<Guid> InsertSerializedVariantAsync()
    {
        Product product = await InsertProductAsync();

        await using PenguinPlankDbContext context = _fixture.CreateContext();
        ProductVariant variant = NewVariant(product.Id, sku: $"SKU-{Guid.NewGuid():N}");
        variant.TrackingMode = TrackingMode.Serialized;
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync();
        return variant.Id;
    }

    private async Task<Guid> InsertCareProfileAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        CareProfile profile = new()
        {
            Id = Guid.NewGuid(),
            Name = $"Care {Guid.NewGuid():N}",
            ActiveFlag = true,
        };
        context.CareProfiles.Add(profile);
        await context.SaveChangesAsync();
        return profile.Id;
    }

    private async Task<Guid> InsertConnectionAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        IntegrationConnection connection = new()
        {
            Id = Guid.NewGuid(),
            Platform = "Shopify",
            Account = $"shop-{Guid.NewGuid():N}",
            Scopes = "read_orders",
            ApiVersion = "2025-01",
            EnabledCapabilities = string.Empty,
            SyncDirection = SyncDirection.None,
            Health = ConnectionHealth.Unknown,
            Enabled = false,
        };
        context.IntegrationConnections.Add(connection);
        await context.SaveChangesAsync();
        return connection.Id;
    }

    private static Product NewProduct() => new()
    {
        Id = Guid.NewGuid(),
        Name = "End-grain cutting board",
        PublicationState = PublicationState.Draft,
        ActiveFlag = true,
    };

    private static ProductVariant NewVariant(Guid productId, string sku) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        Sku = sku,
        TrackingMode = TrackingMode.Quantity,
        UnitOfMeasure = "each",
    };

    private static ProductPiece NewPiece(Guid variantId, string? pieceCode) => new()
    {
        Id = Guid.NewGuid(),
        VariantId = variantId,
        PieceCode = pieceCode,
        PublicationState = PublicationState.Draft,
        ActiveFlag = true,
    };

    private static IntegrationInbox NewInbox(Guid connectionId, string deliveryKey) => new()
    {
        Id = Guid.NewGuid(),
        ConnectionId = connectionId,
        DeliveryKey = deliveryKey,
        PayloadReference = "payload-ref",
        SignatureState = "valid",
        Status = InboxStatus.Received,
        ReceivedAtUtc = DateTimeOffset.UtcNow,
    };

    // SQL Server error 2601 = duplicate key row; 2627 = unique constraint/PK violation.
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Errors.Cast<SqlError>().Any(e => e.Number is 2601 or 2627);

    // SQL Server error 547 = foreign-key constraint conflict (INSERT, DELETE, or UPDATE).
    private static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Errors.Cast<SqlError>().Any(e => e.Number == 547);
}
