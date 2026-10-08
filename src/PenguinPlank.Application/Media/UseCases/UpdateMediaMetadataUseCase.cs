using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media.UseCases;

/// <summary>
/// Edits a stored media asset's editable metadata — caption, role, sort order, and visibility —
/// and audits the change (requirement 6.11 / A4). The storage key, MIME type, size, and checksum
/// describe the stored binary and are never changed here.
/// </summary>
/// <remarks>
/// <para>
/// The use case loads and updates through <see cref="IMediaAssetStore"/>, stamping the modification
/// instant from the injected clock, and records an audit entry on success (coding-standards §1,
/// §2). A missing asset is an expected business failure returned as
/// <see cref="ErrorCode.Validation"/> without persisting or auditing.
/// </para>
/// <para>
/// Setting the visibility to <see cref="MediaVisibility.PublicApproved"/> marks the asset as
/// eligible for a public projection; it is metadata only and never grants anonymous download
/// access — every download is still authorized at the API boundary (requirement 6.11).
/// </para>
/// </remarks>
public sealed class UpdateMediaMetadataUseCase
{
    private readonly IMediaAssetStore _mediaAssetStore;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="mediaAssetStore">The metadata persistence boundary.</param>
    /// <param name="auditSink">The audit boundary a successful edit records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the modification instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public UpdateMediaMetadataUseCase(
        IMediaAssetStore mediaAssetStore,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(mediaAssetStore);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _mediaAssetStore = mediaAssetStore;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the metadata edit.</summary>
    /// <param name="request">The editable metadata to apply.</param>
    /// <param name="actor">The authenticated actor performing the edit.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the updated <see cref="MediaAssetView"/>, or a
    /// <see cref="ErrorCode.Validation"/> failure when no asset matches the id (nothing is persisted
    /// or audited in that case).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<MediaAssetView>> ExecuteAsync(
        UpdateMediaMetadataRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();

        MediaAssetView? updated = await _mediaAssetStore
            .UpdateMetadataAsync(request, updatedAtUtc, cancellationToken)
            .ConfigureAwait(false);

        if (updated is null)
        {
            return Result.Failure<MediaAssetView>(ErrorCode.Validation, "No media asset matches the supplied id.");
        }

        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(
                        nameof(MediaAsset),
                        updated.MediaAssetId,
                        AuditOperation.Updated,
                        [nameof(MediaAsset.Caption), nameof(MediaAsset.Role), nameof(MediaAsset.SortOrder), nameof(MediaAsset.Visibility)]),
                    actor,
                    updatedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(updated);
    }
}
