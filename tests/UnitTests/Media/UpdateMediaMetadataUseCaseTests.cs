using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Media;
using PenguinPlank.Application.Media.UseCases;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Media;

namespace UnitTests.Media;

/// <summary>
/// Use-case behavior of <see cref="UpdateMediaMetadataUseCase"/> exercised with hand-written
/// substitutes and an injected <see cref="MutableTimeProvider"/> (task 9.2): a successful edit
/// applies the caption/role/sort-order/visibility, stamps the modification instant from the clock,
/// audits the update, and returns the view; a missing asset returns a not-found-style validation
/// failure without auditing; and an already-cancelled token short-circuits.
/// </summary>
public sealed class UpdateMediaMetadataUseCaseTests
{
    private static readonly Guid s_assetId = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly ActorContext s_actor = new(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), Role.Owner);

    private readonly FakeMediaAssetStore _mediaAssetStore = new();
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(MediaUseCaseFakes.FixedNow);

    private UpdateMediaMetadataUseCase CreateUseCase() =>
        new(_mediaAssetStore, _auditSink, _timeProvider);

    private static UpdateMediaMetadataRequest Request(MediaVisibility visibility = MediaVisibility.PublicApproved) => new()
    {
        MediaAssetId = s_assetId,
        Caption = "A walnut cutting board",
        Role = "Primary",
        SortOrder = 2,
        Visibility = visibility,
    };

    private static MediaAssetView UpdatedView(MediaVisibility visibility) => new()
    {
        MediaAssetId = s_assetId,
        StorageKey = "random-key-11",
        MimeType = "image/png",
        SizeBytes = 2048,
        Checksum = "sum",
        Caption = "A walnut cutting board",
        Role = "Primary",
        SortOrder = 2,
        Visibility = visibility,
        CreatedAtUtc = MediaUseCaseFakes.FixedNow.AddDays(-1),
        UpdatedAtUtc = MediaUseCaseFakes.FixedNow,
    };

    [Fact]
    public async Task ExecuteAsync_ExistingAsset_AppliesMetadataStampsInstantAndAuditsAsync()
    {
        // Arrange
        _mediaAssetStore.UpdateResult = UpdatedView(MediaVisibility.PublicApproved);
        UpdateMediaMetadataUseCase useCase = CreateUseCase();

        // Act
        Result<MediaAssetView> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(MediaVisibility.PublicApproved, result.Value.Visibility);
        Assert.Equal("Primary", result.Value.Role);

        // The edit was persisted with the clock-supplied instant.
        Assert.NotNull(_mediaAssetStore.AppliedUpdate);
        Assert.Equal(MediaUseCaseFakes.FixedNow, _mediaAssetStore.AppliedUpdate!.Value.UpdatedAtUtc);
        Assert.Equal(2, _mediaAssetStore.AppliedUpdate.Value.Request.SortOrder);

        // The update was audited.
        Assert.Single(_auditSink.Entries);
        Assert.Equal(s_assetId, _auditSink.Entries[0].EntityId);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAsset_ReturnsValidationFailureWithoutAuditingAsync()
    {
        // Arrange: the store reports no asset matched.
        _mediaAssetStore.UpdateResult = null;
        UpdateMediaMetadataUseCase useCase = CreateUseCase();

        // Act
        Result<MediaAssetView> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _mediaAssetStore.UpdateResult = UpdatedView(MediaVisibility.Private);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        UpdateMediaMetadataUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(Request(), s_actor, cts.Token));

        Assert.Empty(_auditSink.Entries);
    }
}
