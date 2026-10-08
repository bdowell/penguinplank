using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Media;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Domain.Common;

namespace UnitTests.Media;

/// <summary>
/// Hand-written controllable substitutes for the boundaries the media use cases depend on — the
/// file store, the metadata store, the audit sink, the identifier generator, and the clock (task
/// 9.2).
/// </summary>
/// <remarks>
/// Each fake is a focused substitute over exactly the interface its use case depends on
/// (coding-standards §2, §7): no EF <c>DbSet</c>/<c>IQueryable</c> is mocked, and no live SQL,
/// network, file system, or real clock is involved. The fakes record what the use case decided (the
/// stored upload, the inserted metadata, the enlisted audit entry) so a test asserts observable
/// outcomes rather than private call order, and each honours an already-cancelled token so the
/// cancellation cases are deterministic.
/// </remarks>
internal static class MediaUseCaseFakes
{
    /// <summary>A fixed, recognizable creation instant the tests assert the use cases stamp from the clock.</summary>
    public static readonly DateTimeOffset FixedNow = new(2024, 7, 1, 9, 30, 0, TimeSpan.Zero);
}

/// <summary>A controllable <see cref="TimeProvider"/> whose current instant the test sets explicitly.</summary>
internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public MutableTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
}

/// <summary>A deterministic <see cref="IIdentifierGenerator"/> handing back a known identifier.</summary>
internal sealed class FakeIdentifierGenerator : IIdentifierGenerator
{
    private readonly Guid _id;

    public FakeIdentifierGenerator(Guid id) => _id = id;

    public int CallCount { get; private set; }

    public Guid NewId()
    {
        CallCount++;
        return _id;
    }
}

/// <summary>A recording <see cref="IAuditSink"/> that captures the entries a use case enlists.</summary>
internal sealed class RecordingAuditSink : IAuditSink
{
    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> Entries => _entries;

    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Controllable substitute for <see cref="IFileStore"/>. The test arranges the save/open outcomes;
/// the fake records the upload it was handed so a test asserts the use case delegated validation
/// and storage to the store rather than re-implementing it.
/// </summary>
internal sealed class FakeFileStore : IFileStore
{
    public Result<StoredMedia>? SaveResult { get; set; }

    public Result<MediaContent>? OpenResult { get; set; }

    /// <summary>The upload the use case handed the store, or <see langword="null"/> when none was.</summary>
    public MediaUpload? SavedUpload { get; private set; }

    /// <summary>The storage key the use case asked to open, or <see langword="null"/> when none was.</summary>
    public StorageKey? OpenedKey { get; private set; }

    public Task<Result<StoredMedia>> SaveAsync(MediaUpload upload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SavedUpload = upload;
        return Task.FromResult(SaveResult ?? throw new InvalidOperationException("Arrange SaveResult."));
    }

    public Task<Result<MediaContent>> OpenAsync(StorageKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenedKey = key;
        return Task.FromResult(OpenResult ?? throw new InvalidOperationException("Arrange OpenResult."));
    }

    public Task<Result> DeleteAsync(StorageKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result.Success());
    }
}

/// <summary>
/// Controllable substitute for <see cref="IMediaAssetStore"/>. The test arranges the read/update
/// outcomes; the fake records the inserted record and the applied update so a test asserts the
/// decided metadata without a database.
/// </summary>
internal sealed class FakeMediaAssetStore : IMediaAssetStore
{
    public MediaAssetView? GetResult { get; set; }

    public MediaAssetView? UpdateResult { get; set; }

    /// <summary>The record the use case decided and inserted, or <see langword="null"/> when none was.</summary>
    public NewMediaAssetRecord? InsertedRecord { get; private set; }

    /// <summary>The update the use case applied, with the stamped instant, or <see langword="null"/> when none was.</summary>
    public (UpdateMediaMetadataRequest Request, DateTimeOffset UpdatedAtUtc)? AppliedUpdate { get; private set; }

    public Task InsertAsync(NewMediaAssetRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InsertedRecord = record;
        return Task.CompletedTask;
    }

    public Task<MediaAssetView?> GetAsync(Guid mediaAssetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetResult);
    }

    public Task<MediaAssetView?> UpdateMetadataAsync(
        UpdateMediaMetadataRequest request,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AppliedUpdate = (request, updatedAtUtc);
        return Task.FromResult(UpdateResult);
    }
}
