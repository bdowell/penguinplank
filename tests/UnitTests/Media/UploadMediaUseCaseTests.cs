using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Media;
using PenguinPlank.Application.Media.UseCases;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Media;

namespace UnitTests.Media;

/// <summary>
/// Use-case behavior of <see cref="UploadMediaUseCase"/> exercised with hand-written controllable
/// substitutes and an injected <see cref="MutableTimeProvider"/> (task 9.2): a success delegates
/// validation/storage to the file store, persists <c>Private</c> metadata with the stored facts,
/// audits the create, and returns the view; a rejected upload returns the store's
/// <c>UploadRejected</c> failure unchanged without persisting or auditing; and an already-cancelled
/// token short-circuits before any work (requirements 6.8, 6.9, 6.10, 6.11 / A4).
/// </summary>
public sealed class UploadMediaUseCaseTests
{
    private static readonly Guid s_newAssetId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly ActorContext s_actor = new(new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Role.Owner);

    private readonly FakeFileStore _fileStore = new();
    private readonly FakeMediaAssetStore _mediaAssetStore = new();
    private readonly FakeIdentifierGenerator _identifierGenerator = new(s_newAssetId);
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(MediaUseCaseFakes.FixedNow);

    private UploadMediaUseCase CreateUseCase() =>
        new(_fileStore, _mediaAssetStore, _identifierGenerator, _auditSink, _timeProvider);

    private static MediaUpload Upload() =>
        new("image/png", 1234, new MemoryStream([0x89, 0x50, 0x4E, 0x47]), "photo.png");

    private static StoredMedia Stored() =>
        new(new StorageKey("random-key-7f3a"), "image/png", 1234, MediaCategory.Image, "checksum-abc");

    [Fact]
    public async Task ExecuteAsync_ValidUpload_StoresPrivateMetadataAndAuditsAsync()
    {
        // Arrange
        _fileStore.SaveResult = Result.Success(Stored());
        UploadMediaUseCase useCase = CreateUseCase();

        // Act
        Result<MediaAssetView> result = await useCase.ExecuteAsync(Upload(), s_actor, CancellationToken.None);

        // Assert: observable outcome is the stored view built from the file store's facts.
        Assert.True(result.IsSuccess);
        Assert.Equal(s_newAssetId, result.Value.MediaAssetId);
        Assert.Equal("image/png", result.Value.MimeType);
        Assert.Equal(1234, result.Value.SizeBytes);
        Assert.Equal("checksum-abc", result.Value.Checksum);
        Assert.Equal(MediaVisibility.Private, result.Value.Visibility);
        Assert.Equal(MediaUseCaseFakes.FixedNow, result.Value.CreatedAtUtc);

        // Metadata was persisted with the randomized key and a Private default.
        NewMediaAssetRecord inserted = Assert.IsType<NewMediaAssetRecord>(_mediaAssetStore.InsertedRecord);
        Assert.Equal(s_newAssetId, inserted.MediaAssetId);
        Assert.Equal("random-key-7f3a", inserted.StorageKey);
        Assert.Equal(MediaVisibility.Private, inserted.Visibility);
        Assert.Equal(MediaUseCaseFakes.FixedNow, inserted.CreatedAtUtc);

        // Validation and storage were delegated to the file store (not re-implemented here).
        Assert.NotNull(_fileStore.SavedUpload);

        // The create was audited.
        Assert.Single(_auditSink.Entries);
        Assert.Equal(s_newAssetId, _auditSink.Entries[0].EntityId);
    }

    [Fact]
    public async Task ExecuteAsync_UploadRejectedByFileStore_ReturnsFailureWithoutPersistingAsync()
    {
        // Arrange: the store rejects (allowlist/signature/size) with the typed code.
        _fileStore.SaveResult = Result.Failure<StoredMedia>(ErrorCode.UploadRejected, "Executable uploads are rejected.");
        UploadMediaUseCase useCase = CreateUseCase();

        // Act
        Result<MediaAssetView> result = await useCase.ExecuteAsync(Upload(), s_actor, CancellationToken.None);

        // Assert: the store's failure is returned unchanged; no metadata and no audit.
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.UploadRejected, result.Error.Code);
        Assert.Null(_mediaAssetStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
        Assert.Equal(0, _identifierGenerator.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _fileStore.SaveResult = Result.Success(Stored());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        UploadMediaUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(Upload(), s_actor, cts.Token));

        Assert.Null(_mediaAssetStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }
}
