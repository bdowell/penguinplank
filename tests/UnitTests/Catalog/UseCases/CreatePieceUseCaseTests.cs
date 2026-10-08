using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog.UseCases;

/// <summary>
/// Use-case behavior of <see cref="CreatePieceUseCase"/> exercised with hand-written controllable
/// substitutes and an injected <see cref="MutableTimeProvider"/> (task 8.4): a success persists the
/// decided piece in <c>Draft</c>, preserving the care-profile version in effect at production time,
/// and audits it; each business rejection (duplicate piece code, archived variant, invalid
/// dimension, unknown variant, no care version in effect) returns a typed failure without
/// persisting; a boundary piece code casing collision is still rejected; and an already-cancelled
/// token short-circuits the operation (requirements R01 Ãƒâ€šÃ‚Â§1.6, Ãƒâ€šÃ‚Â§1.7, Ãƒâ€šÃ‚Â§1.8, Ãƒâ€šÃ‚Â§1.12; R10 Ãƒâ€šÃ‚Â§2.4).
/// </summary>
public sealed class CreatePieceUseCaseTests
{
    private static readonly Guid s_newPieceId = new("66666666-6666-6666-6666-666666666666");
    private static readonly Guid s_variantId = new("77777777-7777-7777-7777-777777777777");
    private static readonly ActorContext s_actor = new(new Guid("88888888-8888-8888-8888-888888888888"), Role.Owner);

    private readonly FakeCatalogPieceStore _pieceStore = new();
    private readonly FakeIdentifierGenerator _identifierGenerator = new(s_newPieceId);
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(CatalogUseCaseFakes.FixedNow);

    private CreatePieceUseCase CreateUseCase() =>
        new(_pieceStore, _identifierGenerator, _auditSink, _timeProvider);

    private static CreatePieceRequest Request(
        string? pieceCode = null,
        Dimensions? dimensions = null,
        DateTimeOffset? productionDate = null) =>
        new()
        {
            VariantId = s_variantId,
            PieceCode = pieceCode,
            Dimensions = dimensions,
            ProductionDate = productionDate,
        };

    private static PieceCreationFacts Facts(
        bool variantIsActive = true,
        IEnumerable<string?>? existingPieceCodes = null,
        Guid? careProfileId = null,
        IReadOnlyList<CareProfileVersion>? careProfileVersions = null) =>
        new()
        {
            VariantIsActive = variantIsActive,
            VariantTrackingMode = TrackingMode.Serialized,
            CareProfileId = careProfileId,
            CareProfileVersions = careProfileVersions ?? [],
            ExistingPieceCodes = existingPieceCodes?.ToArray() ?? Array.Empty<string?>(),
        };

    [Fact]
    public async Task ExecuteAsync_ValidRequestNoCareProfile_PersistsDraftPieceAndAuditsAsync()
    {
        // Arrange
        _pieceStore.CreationFactsResult = Facts();
        CreatePieceUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(pieceCode: "P-001"), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(s_newPieceId, result.Value);

        NewPieceRecord inserted = Assert.IsType<NewPieceRecord>(_pieceStore.InsertedRecord);
        Assert.Equal(s_newPieceId, inserted.PieceId);
        Assert.Equal(s_variantId, inserted.VariantId);
        Assert.Equal("P-001", inserted.PieceCode);
        Assert.Equal(PublicationState.Draft, inserted.PublicationState);
        Assert.Equal(CatalogUseCaseFakes.FixedNow, inserted.CreatedAtUtc);
        Assert.Null(inserted.CareProfileVersionId);

        AuditEntryAssertions.AssertSingleCreated(_auditSink, nameof(ProductPiece), s_newPieceId, CatalogUseCaseFakes.FixedNow, s_actor);
    }

    [Fact]
    public async Task ExecuteAsync_WithCareProfile_PreservesVersionInEffectAtProductionTimeAsync()
    {
        // Arrange: two versions; the piece produced at FixedNow must preserve the earlier one that
        // was in effect, not a version created afterwards (requirement 2.4).
        Guid careProfileId = Guid.NewGuid();
        Guid earlierVersionId = new("00000000-0000-0000-0000-0000000000a1");
        var versions = new List<CareProfileVersion>
        {
            new()
            {
                Id = earlierVersionId,
                CareProfileId = careProfileId,
                VersionNumber = 1,
                Guidance = "v1",
                CreatedAtUtc = CatalogUseCaseFakes.FixedNow.AddDays(-10),
            },
            new()
            {
                Id = Guid.NewGuid(),
                CareProfileId = careProfileId,
                VersionNumber = 2,
                Guidance = "v2",
                CreatedAtUtc = CatalogUseCaseFakes.FixedNow.AddDays(10),
            },
        };
        _pieceStore.CreationFactsResult = Facts(careProfileId: careProfileId, careProfileVersions: versions);
        CreatePieceUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        NewPieceRecord inserted = Assert.IsType<NewPieceRecord>(_pieceStore.InsertedRecord);
        Assert.Equal(earlierVersionId, inserted.CareProfileVersionId);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicatePieceCode_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _pieceStore.CreationFactsResult = Facts(existingPieceCodes: ["P-001"]);
        CreatePieceUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(pieceCode: "P-001"), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.DuplicatePieceCode, result.Error.Code);
        Assert.Null(_pieceStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_PieceCodeCollidesIgnoringCaseAndWhitespace_RejectsCreationAsync()
    {
        // Arrange: boundary case ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â uniqueness is case-insensitive and trim-insensitive.
        _pieceStore.CreationFactsResult = Facts(existingPieceCodes: ["p-001"]);
        CreatePieceUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(pieceCode: "  P-001 "), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.DuplicatePieceCode, result.Error.Code);
        Assert.Null(_pieceStore.InsertedRecord);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedVariant_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _pieceStore.CreationFactsResult = Facts(variantIsActive: false);
        CreatePieceUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
        Assert.Null(_pieceStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDimension_RejectsBeforeLoadingFactsAsync()
    {
        // Arrange: a dimension with no unit is invalid; the use case rejects before any I/O.
        var dimensions = new Dimensions { Length = 1m, Unit = null };
        CreatePieceUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(dimensions: dimensions), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.InvalidDimension, result.Error.Code);
        Assert.Null(_pieceStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownVariant_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _pieceStore.CreationFactsResult = null;
        CreatePieceUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_pieceStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_NoCareVersionInEffectAtProductionTime_RejectsCreationAsync()
    {
        // Arrange: the only version was created after the production instant, so none is in effect.
        Guid careProfileId = Guid.NewGuid();
        var versions = new List<CareProfileVersion>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CareProfileId = careProfileId,
                VersionNumber = 1,
                Guidance = "v1",
                CreatedAtUtc = CatalogUseCaseFakes.FixedNow.AddDays(5),
            },
        };
        _pieceStore.CreationFactsResult = Facts(careProfileId: careProfileId, careProfileVersions: versions);
        CreatePieceUseCase useCase = CreateUseCase();

        // Act: produce before the only version existed.
        Result<Guid> result = await useCase.ExecuteAsync(
            Request(productionDate: CatalogUseCaseFakes.FixedNow.AddDays(-1)),
            s_actor,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_pieceStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _pieceStore.CreationFactsResult = Facts();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        CreatePieceUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(Request(), s_actor, cts.Token));

        Assert.Null(_pieceStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }
}
