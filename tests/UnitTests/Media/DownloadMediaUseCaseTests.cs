using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Media;
using PenguinPlank.Application.Media.UseCases;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Media;

namespace UnitTests.Media;

/// <summary>
/// Use-case behavior of <see cref="DownloadMediaUseCase"/> (task 9.2): an authenticated actor
/// opens the content through the file store using the asset's randomized key; a public-approved
/// asset is downloaded only for an authenticated authorized actor (public approval is never an
/// anonymous grant — requirement 6.11); the system-worker actor is refused as a download client; a
/// missing asset returns a not-found-style validation failure without opening the store; and an
/// already-cancelled token short-circuits.
/// </summary>
public sealed class DownloadMediaUseCaseTests
{
    private static readonly Guid s_assetId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly ActorContext s_staff = new(new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), Role.Staff);

    private readonly FakeMediaAssetStore _mediaAssetStore = new();
    private readonly FakeFileStore _fileStore = new();

    private DownloadMediaUseCase CreateUseCase() => new(_mediaAssetStore, _fileStore);

    private static MediaAssetView View(MediaVisibility visibility, string storageKey = "random-key-99") => new()
    {
        MediaAssetId = s_assetId,
        StorageKey = storageKey,
        MimeType = "image/png",
        SizeBytes = 512,
        Checksum = "sum",
        SortOrder = 0,
        Visibility = visibility,
        CreatedAtUtc = MediaUseCaseFakes.FixedNow,
        UpdatedAtUtc = MediaUseCaseFakes.FixedNow,
    };

    private static Result<MediaContent> OpenedContent() =>
        Result.Success(new MediaContent(new MemoryStream([1, 2, 3]), "image/png", 3));

    [Fact]
    public async Task ExecuteAsync_AuthenticatedActorPrivateAsset_OpensContentByStorageKeyAsync()
    {
        // Arrange
        _mediaAssetStore.GetResult = View(MediaVisibility.Private, "random-key-private");
        _fileStore.OpenResult = OpenedContent();
        DownloadMediaUseCase useCase = CreateUseCase();

        // Act
        Result<MediaContent> result = await useCase.ExecuteAsync(s_assetId, s_staff, CancellationToken.None);

        // Assert: the content was opened via the asset's randomized storage key.
        Assert.True(result.IsSuccess);
        using MediaContent content = result.Value;
        Assert.Equal("image/png", content.ContentType);
        Assert.Equal("random-key-private", _fileStore.OpenedKey?.Value);
    }

    [Fact]
    public async Task ExecuteAsync_PublicApprovedAsset_StillRequiresAuthenticatedActorAndOpensAsync()
    {
        // Arrange: a public-approved asset is downloaded for an authenticated authorized actor.
        // Public approval is metadata only; it is never consulted as an access grant, so the only
        // reason this succeeds is the authenticated actor (requirement 6.11).
        _mediaAssetStore.GetResult = View(MediaVisibility.PublicApproved);
        _fileStore.OpenResult = OpenedContent();
        DownloadMediaUseCase useCase = CreateUseCase();

        // Act
        Result<MediaContent> result = await useCase.ExecuteAsync(s_assetId, s_staff, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        result.Value.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_SystemWorkerActorPublicApprovedAsset_IsForbiddenWithoutOpeningAsync()
    {
        // Arrange: a non-interactive system actor is not a download client. Even a public-approved
        // asset is refused — public approval does not grant access (requirement 6.11).
        _mediaAssetStore.GetResult = View(MediaVisibility.PublicApproved);
        DownloadMediaUseCase useCase = CreateUseCase();

        // Act
        Result<MediaContent> result = await useCase.ExecuteAsync(s_assetId, ActorContext.SystemWorker(), CancellationToken.None);

        // Assert: forbidden, and the file store was never opened.
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Forbidden, result.Error.Code);
        Assert.Null(_fileStore.OpenedKey);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAsset_ReturnsValidationFailureWithoutOpeningAsync()
    {
        // Arrange
        _mediaAssetStore.GetResult = null;
        DownloadMediaUseCase useCase = CreateUseCase();

        // Act
        Result<MediaContent> result = await useCase.ExecuteAsync(s_assetId, s_staff, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_fileStore.OpenedKey);
    }

    [Fact]
    public async Task ExecuteAsync_FileStoreMissingBackingFile_ReturnsStoreFailureAsync()
    {
        // Arrange: metadata exists but the backing file is gone; the store's failure propagates.
        _mediaAssetStore.GetResult = View(MediaVisibility.Private);
        _fileStore.OpenResult = Result.Failure<MediaContent>(ErrorCode.Validation, "No file for the key.");
        DownloadMediaUseCase useCase = CreateUseCase();

        // Act
        Result<MediaContent> result = await useCase.ExecuteAsync(s_assetId, s_staff, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _mediaAssetStore.GetResult = View(MediaVisibility.Private);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        DownloadMediaUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(s_assetId, s_staff, cts.Token));

        Assert.Null(_fileStore.OpenedKey);
    }
}
