using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog.UseCases;

/// <summary>
/// Use-case behavior of <see cref="SetWoodCompositionUseCase"/> exercised with hand-written
/// controllable substitutes and an injected <see cref="MutableTimeProvider"/> (task 8.4): a valid
/// replacement persists exactly the supplied components and audits the change, an empty component
/// set clears the composition, an out-of-range proportion and an archived or unknown target are
/// rejected without replacing anything, boundary proportions are accepted, and an already-cancelled
/// token short-circuits the operation (requirements R01 Ãƒâ€šÃ‚Â§1.5, Ãƒâ€šÃ‚Â§1.8; R10 Ãƒâ€šÃ‚Â§2.2).
/// </summary>
public sealed class SetWoodCompositionUseCaseTests
{
    private static readonly Guid s_variantId = new("99999999-9999-9999-9999-999999999999");
    private static readonly ActorContext s_actor = new(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Role.Owner);

    private readonly FakeCatalogWoodStore _woodStore = new();
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(CatalogUseCaseFakes.FixedNow);

    private SetWoodCompositionUseCase CreateUseCase() =>
        new(_woodStore, _auditSink, _timeProvider);

    private static SetWoodCompositionRequest Request(params WoodComponent[] components) =>
        new(CatalogRef.Variant(s_variantId), components);

    [Fact]
    public async Task ExecuteAsync_ValidComponentsOnActiveTarget_ReplacesCompositionAndAuditsAsync()
    {
        // Arrange
        _woodStore.TargetActiveResult = true;
        var component = new WoodComponent(Guid.NewGuid(), 60m);
        SetWoodCompositionUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ExecuteAsync(Request(component), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(_woodStore.Replacement);
        Assert.Equal(s_variantId, _woodStore.Replacement!.Value.Target.Id);
        Assert.Equal(CatalogEntityKind.Variant, _woodStore.Replacement.Value.Target.Kind);
        WoodComponent persisted = Assert.Single(_woodStore.Replacement.Value.Components);
        Assert.Equal(component.WoodSpeciesId, persisted.WoodSpeciesId);
        Assert.Equal(CatalogUseCaseFakes.FixedNow, _woodStore.Replacement.Value.UpdatedAtUtc);

        AuditEntryAssertions.AssertSingleUpdated(_auditSink, nameof(ProductVariant), s_variantId, CatalogUseCaseFakes.FixedNow, s_actor);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyComponentSetOnActiveTarget_ClearsCompositionAndSucceedsAsync()
    {
        // Arrange: an empty set is the explicit "clear the composition" request.
        _woodStore.TargetActiveResult = true;
        SetWoodCompositionUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(_woodStore.Replacement);
        Assert.Empty(_woodStore.Replacement!.Value.Components);
        Assert.Single(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_BoundaryProportionsZeroAndHundred_AcceptsReplacementAsync()
    {
        // Arrange: 0 and 100 are the inclusive bounds of the valid proportion range.
        _woodStore.TargetActiveResult = true;
        SetWoodCompositionUseCase useCase = CreateUseCase();
        var components = new[]
        {
            new WoodComponent(Guid.NewGuid(), WoodCompositionPolicy.MinimumProportion),
            new WoodComponent(Guid.NewGuid(), WoodCompositionPolicy.MaximumProportion),
        };

        // Act
        Result result = await useCase.ExecuteAsync(Request(components), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(_woodStore.Replacement);
        Assert.Equal(2, _woodStore.Replacement!.Value.Components.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ProportionBelowZero_RejectsWithoutReplacingAsync()
    {
        // Arrange
        _woodStore.TargetActiveResult = true;
        var component = new WoodComponent(Guid.NewGuid(), -0.01m);
        SetWoodCompositionUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ExecuteAsync(Request(component), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.InvalidProportion, result.Error.Code);
        Assert.Null(_woodStore.Replacement);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedTarget_RejectsWithoutReplacingAsync()
    {
        // Arrange
        _woodStore.TargetActiveResult = false;
        var component = new WoodComponent(Guid.NewGuid(), 50m);
        SetWoodCompositionUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ExecuteAsync(Request(component), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
        Assert.Null(_woodStore.Replacement);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTarget_RejectsWithoutReplacingAsync()
    {
        // Arrange
        _woodStore.TargetActiveResult = null;
        var component = new WoodComponent(Guid.NewGuid(), 50m);
        SetWoodCompositionUseCase useCase = CreateUseCase();

        // Act
        Result result = await useCase.ExecuteAsync(Request(component), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_woodStore.Replacement);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _woodStore.TargetActiveResult = true;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var component = new WoodComponent(Guid.NewGuid(), 50m);
        SetWoodCompositionUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(Request(component), s_actor, cts.Token));

        Assert.Null(_woodStore.Replacement);
        Assert.Empty(_auditSink.Entries);
    }
}
