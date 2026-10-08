using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog.UseCases;

/// <summary>
/// Use-case behavior of <see cref="ArchiveVariantUseCase"/> exercised with hand-written controllable
/// substitutes and an injected <see cref="MutableTimeProvider"/> (task 8.4): archiving an active
/// variant flips the flag and audits it, the operation is idempotent against the current state
/// (an already-archived no-op persists nothing yet still succeeds), an unknown variant is rejected,
/// and an already-cancelled token short-circuits the operation (requirement R01 Ãƒâ€šÃ‚Â§1.8).
/// </summary>
public sealed class ArchiveVariantUseCaseTests
{
    private static readonly Guid s_variantId = new("44444444-4444-4444-4444-444444444444");
    private static readonly ActorContext s_actor = new(new Guid("55555555-5555-5555-5555-555555555555"), Role.Owner);

    private readonly FakeCatalogVariantStore _variantStore = new();
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(CatalogUseCaseFakes.FixedNow);

    private ArchiveVariantUseCase CreateUseCase() =>
        new(_variantStore, _auditSink, _timeProvider);

    private static VariantFacts FactsWithActive(bool isActive) => new()
    {
        VariantId = s_variantId,
        ProductId = Guid.NewGuid(),
        IsActive = isActive,
        TrackingMode = TrackingMode.Serialized,
        HasStockHistory = false,
        RowVersion = [1, 2, 3, 4],
    };

    [Fact]
    public async Task ArchiveAsync_ActiveVariant_FlipsActiveFlagToFalseAndAuditsAsync()
    {
        // Arrange
        _variantStore.VariantFactsResult = FactsWithActive(isActive: true);
        ArchiveVariantUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ArchiveAsync(s_variantId, s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(_variantStore.ActiveFlagChange);
        Assert.Equal(s_variantId, _variantStore.ActiveFlagChange!.Value.VariantId);
        Assert.False(_variantStore.ActiveFlagChange.Value.IsActive);
        Assert.Equal(CatalogUseCaseFakes.FixedNow, _variantStore.ActiveFlagChange.Value.UpdatedAtUtc);

        AuditEntryAssertions.AssertSingleUpdated(_auditSink, nameof(ProductVariant), s_variantId, CatalogUseCaseFakes.FixedNow, s_actor);
    }

    [Fact]
    public async Task UnarchiveAsync_ArchivedVariant_FlipsActiveFlagToTrueAndAuditsAsync()
    {
        // Arrange
        _variantStore.VariantFactsResult = FactsWithActive(isActive: false);
        ArchiveVariantUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.UnarchiveAsync(s_variantId, s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(_variantStore.ActiveFlagChange);
        Assert.True(_variantStore.ActiveFlagChange!.Value.IsActive);
        Assert.Single(_auditSink.Entries);
    }

    [Fact]
    public async Task ArchiveAsync_AlreadyArchivedVariant_IsIdempotentNoOpThatSucceedsAsync()
    {
        // Arrange: archiving a variant that is already archived is a safe no-op (boundary case).
        _variantStore.VariantFactsResult = FactsWithActive(isActive: false);
        ArchiveVariantUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ArchiveAsync(s_variantId, s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(_variantStore.ActiveFlagChange);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task UnarchiveAsync_AlreadyActiveVariant_IsIdempotentNoOpThatSucceedsAsync()
    {
        // Arrange: reactivating an already-active variant is a safe no-op (boundary case).
        _variantStore.VariantFactsResult = FactsWithActive(isActive: true);
        ArchiveVariantUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.UnarchiveAsync(s_variantId, s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(_variantStore.ActiveFlagChange);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ArchiveAsync_UnknownVariant_RejectsWithoutPersistingAsync()
    {
        // Arrange
        _variantStore.VariantFactsResult = null;
        ArchiveVariantUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ArchiveAsync(s_variantId, s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_variantStore.ActiveFlagChange);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ArchiveAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _variantStore.VariantFactsResult = FactsWithActive(isActive: true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        ArchiveVariantUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ArchiveAsync(s_variantId, s_actor, cts.Token));

        Assert.Null(_variantStore.ActiveFlagChange);
        Assert.Empty(_auditSink.Entries);
    }
}
