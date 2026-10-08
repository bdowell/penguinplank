using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.Auditing;
using PenguinPlank.Infrastructure.Catalog.Persistence;
using PenguinPlank.Infrastructure.Persistence;
using PenguinPlank.Infrastructure.Persistence.Concurrency;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests exercising the <b>real</b> EF Core catalog persistence adapters (task 8.3)
/// and the catalog use cases/writer (task 8.2) against a real isolated SQL Server database built
/// from the <b>actual</b> committed migration (coding-standards §7). These prove the end-to-end
/// persistence behavior the unit tests (task 8.4) cannot: the relational constraints and the
/// insert-only mapping that enforce the catalog's business rules at the row level
/// (requirements R01 §1.1, §1.2, §1.7, §1.8; R10 §2.3, §2.5).
/// </summary>
/// <remarks>
/// <para>
/// Each test wires the concrete stores (<see cref="EfCatalogVariantStore"/>,
/// <see cref="EfCatalogPieceStore"/>, <see cref="EfCareProfileVersionStore"/>), use cases, and the
/// <see cref="CatalogWriter"/> over a <see cref="PenguinPlankDbContext"/> bound to the fixture's
/// isolated database — the same scoped context flows through the writer, its use cases, the
/// <see cref="EfAuditSink"/>, and the <see cref="EfConcurrentUpdateExecutor"/>, exactly as the
/// scoped composition root would wire them. Time is a controllable <see cref="FixedTimeProvider"/>
/// and identifiers come from a deterministic <see cref="SequentialIdentifierGenerator"/>, so an
/// assertion can name the exact id and instant a create produced (coding-standards §1, §2). No test
/// mocks a <c>DbSet</c>/<c>IQueryable</c>, because that cannot prove relational correctness.
/// </para>
/// </remarks>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class CatalogPersistenceTests
{
    private static readonly ActorContext s_owner = new(Guid.NewGuid(), Role.Owner);

    private readonly SqlServerDatabaseFixture _fixture;

    public CatalogPersistenceTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================================
    // Create / archive flows via the real writer + stores against the actual migration.
    // =========================================================================================

    [Fact]
    public async Task CreateProductVariantPiece_ThroughWriter_PersistsTheWholeGraphAsync()
    {
        Guid productId;
        Guid variantId;
        Guid pieceId;

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            CatalogWriter writer = CreateWriter(context);

            Result<Guid> productResult = await writer.CreateProductAsync(
                new CreateProductRequest { Name = "End-grain cutting board" },
                s_owner,
                CancellationToken.None);
            Assert.True(productResult.IsSuccess);
            productId = productResult.Value;

            Result<Guid> variantResult = await writer.CreateVariantAsync(
                new CreateVariantRequest
                {
                    ProductId = productId,
                    Sku = "SKU-WRITER-1",
                    TrackingMode = TrackingMode.Serialized,
                    UnitOfMeasure = "each",
                },
                s_owner,
                CancellationToken.None);
            Assert.True(variantResult.IsSuccess);
            variantId = variantResult.Value;

            Result<Guid> pieceResult = await writer.CreatePieceAsync(
                new CreatePieceRequest { VariantId = variantId, PieceCode = "PC-WRITER-1" },
                s_owner,
                CancellationToken.None);
            Assert.True(pieceResult.IsSuccess);
            pieceId = pieceResult.Value;
        }

        // Verify every row landed in the real database through a fresh context.
        await using PenguinPlankDbContext verify = _fixture.CreateContext();

        Product storedProduct = await verify.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(PublicationState.Draft, storedProduct.PublicationState);
        Assert.True(storedProduct.ActiveFlag);

        ProductVariant storedVariant =
            await verify.ProductVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
        Assert.Equal("SKU-WRITER-1", storedVariant.Sku);
        Assert.Equal(productId, storedVariant.ProductId);
        Assert.Equal(TrackingMode.Serialized, storedVariant.TrackingMode);

        ProductPiece storedPiece = await verify.ProductPieces.AsNoTracking().SingleAsync(p => p.Id == pieceId);
        Assert.Equal("PC-WRITER-1", storedPiece.PieceCode);
        Assert.Equal(variantId, storedPiece.VariantId);
        Assert.Equal(PublicationState.Draft, storedPiece.PublicationState);
    }

    [Fact]
    public async Task ArchiveVariant_ThroughWriter_FlipsActiveFlagAndRejectsNewPieceAsync()
    {
        Guid variantId;

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            CatalogWriter writer = CreateWriter(context);
            Guid productId = (await writer.CreateProductAsync(
                new CreateProductRequest { Name = "Serving tray" }, s_owner, CancellationToken.None)).Value;

            variantId = (await writer.CreateVariantAsync(
                new CreateVariantRequest
                {
                    ProductId = productId,
                    Sku = "SKU-ARCHIVE-1",
                    TrackingMode = TrackingMode.Serialized,
                    UnitOfMeasure = "each",
                },
                s_owner,
                CancellationToken.None)).Value;

            Result archiveResult =
                await writer.ArchiveAsync(CatalogRef.Variant(variantId), s_owner, CancellationToken.None);
            Assert.True(archiveResult.IsSuccess);
        }

        // The archive is persisted as ActiveFlag = false.
        await using (PenguinPlankDbContext verify = _fixture.CreateContext())
        {
            ProductVariant archived =
                await verify.ProductVariants.AsNoTracking().SingleAsync(v => v.Id == variantId);
            Assert.False(archived.ActiveFlag);
        }

        // The archived guard rejects a new transaction (creating a piece) against the variant.
        await using (PenguinPlankDbContext guarded = _fixture.CreateContext())
        {
            CatalogWriter writer = CreateWriter(guarded);
            Result<Guid> pieceResult = await writer.CreatePieceAsync(
                new CreatePieceRequest { VariantId = variantId, PieceCode = "PC-ARCHIVED-1" },
                s_owner,
                CancellationToken.None);

            Assert.True(pieceResult.IsFailure);
            Assert.Equal(ErrorCode.ArchivedRecord, pieceResult.Error.Code);
        }

        // No piece row was created by the rejected transaction.
        await using PenguinPlankDbContext confirm = _fixture.CreateContext();
        Assert.False(await confirm.ProductPieces.AnyAsync(p => p.VariantId == variantId));
    }

    [Fact]
    public async Task UpdateProduct_WithStaleIfMatch_IsRejectedByRowVersionAndDoesNotOverwriteAsync()
    {
        Guid productId;
        string staleEtag;

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            CatalogWriter writer = CreateWriter(context);
            productId = (await writer.CreateProductAsync(
                new CreateProductRequest { Name = "Original" }, s_owner, CancellationToken.None)).Value;
        }

        // The client reads the product and keeps its ETag (If-Match token).
        await using (PenguinPlankDbContext read = _fixture.CreateContext())
        {
            Product loaded = await read.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
            staleEtag = ETag.Encode(loaded.RowVersion);
        }

        // A concurrent edit advances the stored row version.
        await using (PenguinPlankDbContext concurrent = _fixture.CreateContext())
        {
            Product tracked = await concurrent.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "Winner";
            await concurrent.SaveChangesAsync();
        }

        // The original client's edit, presenting the now-stale token, must be refused.
        await using (PenguinPlankDbContext editContext = _fixture.CreateContext())
        {
            CatalogWriter writer = CreateWriter(editContext);
            Result result = await writer.UpdateProductAsync(
                new UpdateProductRequest
                {
                    ProductId = productId,
                    ExpectedVersion = staleEtag,
                    Name = "Loser",
                },
                s_owner,
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCode.StaleVersion, result.Error.Code);
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Product stored = await verify.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal("Winner", stored.Name);
    }

    // =========================================================================================
    // Care-version append flows via the real store + use case (requirement 2.3).
    // =========================================================================================

    [Fact]
    public async Task AddCareVersion_ThroughUseCase_IncrementsVersionNumbersAndPreservesPriorVersionsAsync()
    {
        Guid careProfileId = await InsertCareProfileAsync();

        CareProfileVersionView first;
        CareProfileVersionView second;
        CareProfileVersionView third;

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            AddCareVersionUseCase useCase = CreateAddCareVersionUseCase(context);

            first = (await useCase.ExecuteAsync(
                new AddCareVersionRequest(careProfileId, "Hand wash only."),
                s_owner,
                CancellationToken.None)).Value;
        }

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            second = (await CreateAddCareVersionUseCase(context).ExecuteAsync(
                new AddCareVersionRequest(careProfileId, "Hand wash and re-oil monthly."),
                s_owner,
                CancellationToken.None)).Value;
        }

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            third = (await CreateAddCareVersionUseCase(context).ExecuteAsync(
                new AddCareVersionRequest(careProfileId, "Re-oil quarterly."),
                s_owner,
                CancellationToken.None)).Value;
        }

        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(3, third.VersionNumber);

        // Every appended version persisted; the earliest kept its original guidance verbatim.
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        List<CareProfileVersion> versions = await verify.CareProfileVersions
            .AsNoTracking()
            .Where(v => v.CareProfileId == careProfileId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();

        Assert.Equal(3, versions.Count);
        Assert.Equal("Hand wash only.", versions[0].Guidance);
        Assert.Equal("Re-oil quarterly.", versions[2].Guidance);
    }

    // =========================================================================================
    // Duplicate-code rejection enforced at the DATABASE level (requirements 1.1/1.2/1.7).
    // =========================================================================================

    [Fact]
    public async Task InsertVariant_DuplicateSku_IsRejectedByTheUniqueIndexAsync()
    {
        Guid productId = await InsertProductRowAsync();

        // First variant with the SKU persists through the real store.
        await using (PenguinPlankDbContext firstContext = _fixture.CreateContext())
        {
            var firstStore = new EfCatalogVariantStore(firstContext);
            await firstStore.InsertVariantAsync(
                NewVariantRecordFor(productId, "SKU-DB-DUP-1"), CancellationToken.None);
        }

        // A second variant presenting the same SKU is rejected by the relational unique index —
        // not merely by the application pre-check — proving the constraint exists in the schema.
        await using PenguinPlankDbContext secondContext = _fixture.CreateContext();
        var secondStore = new EfCatalogVariantStore(secondContext);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            secondStore.InsertVariantAsync(
                NewVariantRecordFor(productId, "SKU-DB-DUP-1"), CancellationToken.None));

        Assert.True(IsUniqueConstraintViolation(exception));
    }

    [Fact]
    public async Task InsertPiece_DuplicatePieceCode_IsRejectedByTheFilteredUniqueIndexAsync()
    {
        Guid variantId = await InsertSerializedVariantRowAsync();

        await using (PenguinPlankDbContext firstContext = _fixture.CreateContext())
        {
            var firstStore = new EfCatalogPieceStore(firstContext);
            await firstStore.InsertPieceAsync(NewPieceRecordFor(variantId, "PC-DB-DUP-1"), CancellationToken.None);
        }

        await using PenguinPlankDbContext secondContext = _fixture.CreateContext();
        var secondStore = new EfCatalogPieceStore(secondContext);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            secondStore.InsertPieceAsync(NewPieceRecordFor(variantId, "PC-DB-DUP-1"), CancellationToken.None));

        Assert.True(IsUniqueConstraintViolation(exception));
    }

    // =========================================================================================
    // Care-version immutability enforced at the ROW level (requirements 2.3, 2.5): an existing
    // CareProfileVersion row cannot be updated or deleted per the insert-only mapping. The entity
    // derives from Entity (no rowversion) and the configuration exposes no update/delete path;
    // these tests prove the row survives attempts to mutate or remove it.
    // =========================================================================================

    [Fact]
    public async Task CareProfileVersion_AttemptedGuidanceEdit_ThrowsAndLeavesStoredGuidanceUnchangedAsync()
    {
        Guid careProfileId = await InsertCareProfileAsync();
        CareProfileVersion appended = await AppendCareVersionRowAsync(careProfileId, 1, "Original guidance.");

        // Attempt to mutate the existing immutable row directly through EF. The insert-only mapping
        // blocks post-insert edits to the business columns, so SaveChanges throws rather than
        // rewriting history (requirements 2.3, 2.5).
        await using (PenguinPlankDbContext mutateContext = _fixture.CreateContext())
        {
            CareProfileVersion tracked =
                await mutateContext.CareProfileVersions.SingleAsync(v => v.Id == appended.Id);
            tracked.Guidance = "Tampered guidance.";

            await Assert.ThrowsAsync<InvalidOperationException>(() => mutateContext.SaveChangesAsync());
        }

        // The original row survives the blocked edit with its guidance intact.
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        CareProfileVersion preserved =
            await verify.CareProfileVersions.AsNoTracking().SingleAsync(v => v.Id == appended.Id);

        Assert.Equal("Original guidance.", preserved.Guidance);
        Assert.Equal(1, preserved.VersionNumber);
    }

    [Fact]
    public async Task CareProfileVersion_AttemptedVersionNumberEdit_ThrowsAndLeavesTheRowUnchangedAsync()
    {
        Guid careProfileId = await InsertCareProfileAsync();
        CareProfileVersion appended = await AppendCareVersionRowAsync(careProfileId, 1, "Immutable guidance.");

        await using (PenguinPlankDbContext mutateContext = _fixture.CreateContext())
        {
            CareProfileVersion tracked =
                await mutateContext.CareProfileVersions.SingleAsync(v => v.Id == appended.Id);
            tracked.VersionNumber = 99;

            await Assert.ThrowsAsync<InvalidOperationException>(() => mutateContext.SaveChangesAsync());
        }

        // The historical row remains readable with its original number — a produced piece must keep
        // resolving the exact version in effect at its production time (requirement 2.4).
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        CareProfileVersion preserved =
            await verify.CareProfileVersions.AsNoTracking().SingleAsync(v => v.Id == appended.Id);

        Assert.Equal(1, preserved.VersionNumber);
    }

    [Fact]
    public async Task PiecePreservesCareVersion_AfterLaterCareEdit_StillResolvesProductionTimeVersionAsync()
    {
        // Prove the row-level immutability has the intended effect: a piece produced against
        // version 1 keeps resolving version 1 even after version 2 is appended (requirement 2.4).
        Guid careProfileId = await InsertCareProfileAsync();
        CareProfileVersion versionOne = await AppendCareVersionRowAsync(
            careProfileId, 1, "Care v1.", new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero));

        Guid productId = await InsertProductRowAsync();
        Guid variantId = await InsertSerializedVariantRowAsync(productId, careProfileId);

        Guid pieceId;
        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            CatalogWriter writer = CreateWriter(context);
            Result<Guid> pieceResult = await writer.CreatePieceAsync(
                new CreatePieceRequest { VariantId = variantId, PieceCode = "PC-CARE-1" },
                s_owner,
                CancellationToken.None);
            Assert.True(pieceResult.IsSuccess);
            pieceId = pieceResult.Value;
        }

        // A later care edit appends version 2 (append-only; v1 is untouched).
        await AppendCareVersionRowAsync(
            careProfileId, 2, "Care v2.", new DateTimeOffset(2025, 1, 20, 0, 0, 0, TimeSpan.Zero));

        await using PenguinPlankDbContext resolve = _fixture.CreateContext();
        var store = new CareProfileStore(resolve, CreateAddCareVersionUseCase(resolve));
        CareProfileVersionView? resolved =
            await store.ResolveVersionForPieceAsync(pieceId, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(versionOne.Id, resolved!.CareProfileVersionId);
        Assert.Equal(1, resolved.VersionNumber);
        Assert.Equal("Care v1.", resolved.Guidance);
    }

    // =========================================================================================
    // Wiring helpers — build the real adapters/use cases over a single scoped context, mirroring
    // the scoped composition root. Time and identifiers are controllable (coding-standards §1, §2).
    // =========================================================================================

    private static CatalogWriter CreateWriter(PenguinPlankDbContext context)
    {
        var identifierGenerator = new SequentialIdentifierGenerator();
        var auditSink = new EfAuditSink(context);
        TimeProvider timeProvider = FixedTimeProvider.AtUtc(2025, 1, 15);

        var variantStore = new EfCatalogVariantStore(context);
        var pieceStore = new EfCatalogPieceStore(context);
        var woodStore = new EfCatalogWoodStore(context);

        var createVariant = new CreateVariantUseCase(variantStore, identifierGenerator, auditSink, timeProvider);
        var createPiece = new CreatePieceUseCase(pieceStore, identifierGenerator, auditSink, timeProvider);
        var setWood = new SetWoodCompositionUseCase(woodStore, auditSink, timeProvider);
        var archiveVariant = new ArchiveVariantUseCase(variantStore, auditSink, timeProvider);

        return new CatalogWriter(
            context,
            new EfConcurrentUpdateExecutor(context),
            createVariant,
            createPiece,
            setWood,
            archiveVariant,
            identifierGenerator,
            auditSink,
            timeProvider);
    }

    private static AddCareVersionUseCase CreateAddCareVersionUseCase(PenguinPlankDbContext context) =>
        new(
            new EfCareProfileVersionStore(context),
            new SequentialIdentifierGenerator(),
            new EfAuditSink(context),
            FixedTimeProvider.AtUtc(2025, 1, 15));

    // =========================================================================================
    // Arrange helpers — minimal, explicit fixtures (no shared seeded dataset, §7).
    // =========================================================================================

    private async Task<Guid> InsertProductRowAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = "Board",
            PublicationState = PublicationState.Draft,
            ActiveFlag = true,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product.Id;
    }

    private async Task<Guid> InsertSerializedVariantRowAsync() =>
        await InsertSerializedVariantRowAsync(await InsertProductRowAsync(), careProfileId: null);

    private async Task<Guid> InsertSerializedVariantRowAsync(Guid productId, Guid? careProfileId)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        ProductVariant variant = new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Sku = $"SKU-{Guid.NewGuid():N}",
            TrackingMode = TrackingMode.Serialized,
            UnitOfMeasure = "each",
            CareProfileId = careProfileId,
            ActiveFlag = true,
        };
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

    private Task<CareProfileVersion> AppendCareVersionRowAsync(
        Guid careProfileId,
        int versionNumber,
        string guidance) =>
        AppendCareVersionRowAsync(
            careProfileId,
            versionNumber,
            guidance,
            new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero));

    private async Task<CareProfileVersion> AppendCareVersionRowAsync(
        Guid careProfileId,
        int versionNumber,
        string guidance,
        DateTimeOffset createdAtUtc)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        CareProfileVersion version = new()
        {
            Id = Guid.NewGuid(),
            CareProfileId = careProfileId,
            VersionNumber = versionNumber,
            Guidance = guidance,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };
        context.CareProfileVersions.Add(version);
        await context.SaveChangesAsync();
        return version;
    }

    private static NewVariantRecord NewVariantRecordFor(Guid productId, string sku) => new()
    {
        VariantId = Guid.NewGuid(),
        ProductId = productId,
        Sku = sku,
        TrackingMode = TrackingMode.Quantity,
        UnitOfMeasure = "each",
        PublicationState = PublicationState.Draft,
        CreatedAtUtc = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero),
        WoodComposition = [],
    };

    private static NewPieceRecord NewPieceRecordFor(Guid variantId, string pieceCode) => new()
    {
        PieceId = Guid.NewGuid(),
        VariantId = variantId,
        PieceCode = pieceCode,
        PublicationState = PublicationState.Draft,
        CreatedAtUtc = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero),
    };

    // SQL Server error 2601 = duplicate key row; 2627 = unique constraint/PK violation.
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Errors.Cast<SqlError>().Any(e => e.Number is 2601 or 2627);

    /// <summary>
    /// A deterministic <see cref="TimeProvider"/> reporting a fixed UTC instant so creation
    /// timestamps are predictable in assertions (coding-standards §2).
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _instant;

        private FixedTimeProvider(DateTimeOffset instant)
        {
            _instant = instant;
        }

        public static FixedTimeProvider AtUtc(int year, int month, int day) =>
            new(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero));

        public override DateTimeOffset GetUtcNow() => _instant;
    }

    /// <summary>
    /// A controllable <see cref="IIdentifierGenerator"/> handing out unique, time-ordered GUIDs —
    /// a boundary substitute standing in for the production <c>GuidIdentifierGenerator</c>
    /// (coding-standards §1, §2). It must produce <em>distinct</em> ids across every test because
    /// the collection shares one database, so a fixed/sequential value would collide on the primary
    /// key between tests; the tests assert on the returned id, not a hard-coded value.
    /// </summary>
    private sealed class SequentialIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
