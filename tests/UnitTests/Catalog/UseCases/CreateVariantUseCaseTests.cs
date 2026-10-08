using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog.UseCases;

/// <summary>
/// Use-case behavior of <see cref="CreateVariantUseCase"/> exercised with hand-written controllable
/// substitutes and an injected <see cref="MutableTimeProvider"/> (task 8.4): a success persists the
/// decided record and audits it, each business rejection returns the rejecting policy's typed
/// failure without persisting, a boundary input is accepted, and an already-cancelled token short-
/// circuits the operation (requirements R01 Ãƒâ€šÃ‚Â§1.1, Ãƒâ€šÃ‚Â§1.2, Ãƒâ€šÃ‚Â§1.8, Ãƒâ€šÃ‚Â§1.13).
/// </summary>
public sealed class CreateVariantUseCaseTests
{
    private static readonly Guid s_newVariantId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_productId = new("22222222-2222-2222-2222-222222222222");
    private static readonly ActorContext s_actor = new(new Guid("33333333-3333-3333-3333-333333333333"), Role.Owner);

    private readonly FakeCatalogVariantStore _variantStore = new();
    private readonly FakeIdentifierGenerator _identifierGenerator = new(s_newVariantId);
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(CatalogUseCaseFakes.FixedNow);

    private CreateVariantUseCase CreateUseCase() =>
        new(_variantStore, _identifierGenerator, _auditSink, _timeProvider);

    private static CreateVariantRequest ValidRequest(
        string sku = "SKU-100",
        string? barcode = null,
        Dimensions? dimensions = null,
        IReadOnlyList<WoodComponent>? woodComposition = null) =>
        new()
        {
            ProductId = s_productId,
            Sku = sku,
            TrackingMode = TrackingMode.Quantity,
            UnitOfMeasure = "each",
            Barcode = barcode,
            Dimensions = dimensions,
            WoodComposition = woodComposition ?? [],
        };

    [Fact]
    public async Task ExecuteAsync_ValidRequestUnderActiveProduct_PersistsDecidedVariantAndAuditsAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = true;
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(sku: "SKU-100"), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(s_newVariantId, result.Value);

        NewVariantRecord inserted = Assert.IsType<NewVariantRecord>(_variantStore.InsertedRecord);
        Assert.Equal(s_newVariantId, inserted.VariantId);
        Assert.Equal(s_productId, inserted.ProductId);
        Assert.Equal("SKU-100", inserted.Sku);
        Assert.Equal(PublicationState.Draft, inserted.PublicationState);
        Assert.Equal(CatalogUseCaseFakes.FixedNow, inserted.CreatedAtUtc);

        AuditEntryAssertions.AssertSingleCreated(_auditSink, nameof(ProductVariant), s_newVariantId, CatalogUseCaseFakes.FixedNow, s_actor);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateSku_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = true;
        _variantStore.UniquenessSnapshot = new CatalogUniquenessSnapshot
        {
            ExistingSkus = ["SKU-100"],
            ExistingBarcodes = Array.Empty<string>(),
        };
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(sku: "SKU-100"), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.DuplicateSku, result.Error.Code);
        Assert.Null(_variantStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateBarcode_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = true;
        _variantStore.UniquenessSnapshot = new CatalogUniquenessSnapshot
        {
            ExistingSkus = Array.Empty<string>(),
            ExistingBarcodes = ["BAR-9"],
        };
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(barcode: "BAR-9"), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_variantStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedProduct_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = false;
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
        Assert.Null(_variantStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProduct_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = null;
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_variantStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeDimension_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = true;
        var dimensions = new Dimensions { Length = -1m, Unit = "in" };
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(dimensions: dimensions), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.InvalidDimension, result.Error.Code);
        Assert.Null(_variantStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_ProportionAboveHundred_RejectsCreationWithoutPersistingAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = true;
        var composition = new[] { new WoodComponent(Guid.NewGuid(), 100.01m) };
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(woodComposition: composition), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.InvalidProportion, result.Error.Code);
        Assert.Null(_variantStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_BoundaryProportionsZeroAndHundred_AcceptsCreationAsync()
    {
        // Arrange: 0 and 100 are the inclusive bounds of the valid proportion range.
        _variantStore.ProductActiveResult = true;
        var composition = new[]
        {
            new WoodComponent(Guid.NewGuid(), WoodCompositionPolicy.MinimumProportion),
            new WoodComponent(Guid.NewGuid(), WoodCompositionPolicy.MaximumProportion),
        };
        CreateVariantUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(ValidRequest(woodComposition: composition), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(s_newVariantId, result.Value);
        Assert.NotNull(_variantStore.InsertedRecord);
        Assert.Single(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _variantStore.ProductActiveResult = true;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        CreateVariantUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(ValidRequest(), s_actor, cts.Token));

        Assert.Null(_variantStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }
}
