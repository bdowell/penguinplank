using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media.UseCases;

/// <summary>
/// Validates and stores a media upload, then persists its <c>MediaAsset</c> metadata: it delegates
/// validation and binary storage to <see cref="IFileStore.SaveAsync"/> (allowlist, magic-byte
/// signature, reject executable/HTML, size bound, randomized key) and, on success, records the
/// metadata through <see cref="IMediaAssetStore"/> (requirements 6.8, 6.9, 6.10 / A4).
/// </summary>
/// <remarks>
/// <para>
/// The use case holds the upload orchestration a thin endpoint must not: it calls the file store,
/// returns the store's typed <see cref="ErrorCode.UploadRejected"/> failure unchanged when
/// validation rejects the upload (no metadata is written in that case), and otherwise builds the
/// decided <see cref="NewMediaAssetRecord"/> from the <see cref="StoredMedia"/> facts plus an
/// injected identifier and clock, inserts it, and audits the create (coding-standards §1, §3).
/// Validation itself is <b>not</b> re-implemented here — it lives in the pure
/// <c>MediaUploadValidationPolicy</c> the file store applies (coding-standards §1).
/// </para>
/// <para>
/// A new asset always starts <see cref="MediaVisibility.Private"/>; being public-approved is a
/// later explicit metadata edit and never grants anonymous download access (requirement 6.11). The
/// clock and identifier are injected, so the recorded instant and the returned id are deterministic
/// under test; the use case never reads <c>DateTime.UtcNow</c> or calls <c>Guid.NewGuid</c> inside
/// the decision (coding-standards §2).
/// </para>
/// </remarks>
public sealed class UploadMediaUseCase
{
    private readonly IFileStore _fileStore;
    private readonly IMediaAssetStore _mediaAssetStore;
    private readonly IIdentifierGenerator _identifierGenerator;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="fileStore">The file-store boundary that validates and persists the binary content.</param>
    /// <param name="mediaAssetStore">The metadata persistence boundary.</param>
    /// <param name="identifierGenerator">The generator for the new asset's identifier.</param>
    /// <param name="auditSink">The audit boundary a successful upload records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the creation instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public UploadMediaUseCase(
        IFileStore fileStore,
        IMediaAssetStore mediaAssetStore,
        IIdentifierGenerator identifierGenerator,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(mediaAssetStore);
        ArgumentNullException.ThrowIfNull(identifierGenerator);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _fileStore = fileStore;
        _mediaAssetStore = mediaAssetStore;
        _identifierGenerator = identifierGenerator;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the upload-media orchestration.</summary>
    /// <param name="upload">The upload to validate and store.</param>
    /// <param name="actor">The authenticated actor performing the upload.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the stored asset's <see cref="MediaAssetView"/>, or the file store's typed
    /// <see cref="ErrorCode.UploadRejected"/> failure when validation rejects the upload (no
    /// metadata is written in that case).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="upload"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<MediaAssetView>> ExecuteAsync(
        MediaUpload upload,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(actor);

        // 1. Validate and store the binary content through the file store. A rejected upload is an
        // expected business failure returned unchanged — no metadata is written.
        Result<StoredMedia> stored = await _fileStore
            .SaveAsync(upload, cancellationToken)
            .ConfigureAwait(false);

        if (stored.IsFailure)
        {
            return Result.Failure<MediaAssetView>(stored.Error);
        }

        // 2. The content is stored: build the decided metadata and persist it. A new asset starts
        // Private; public approval is a later explicit edit (requirement 6.11).
        StoredMedia facts = stored.Value;
        Guid mediaAssetId = _identifierGenerator.NewId();
        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();

        var record = new NewMediaAssetRecord
        {
            MediaAssetId = mediaAssetId,
            StorageKey = facts.StorageKey.Value,
            MimeType = facts.ContentType,
            SizeBytes = facts.SizeBytes,
            Checksum = facts.Checksum,
            Caption = upload.OriginalFileName,
            Visibility = MediaVisibility.Private,
            CreatedAtUtc = createdAtUtc,
        };

        await _mediaAssetStore.InsertAsync(record, cancellationToken).ConfigureAwait(false);

        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(nameof(MediaAsset), mediaAssetId, AuditOperation.Created),
                    actor,
                    createdAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        var view = new MediaAssetView
        {
            MediaAssetId = mediaAssetId,
            StorageKey = record.StorageKey,
            MimeType = record.MimeType,
            SizeBytes = record.SizeBytes,
            Checksum = record.Checksum,
            Caption = record.Caption,
            Role = null,
            SortOrder = 0,
            Visibility = MediaVisibility.Private,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };

        return Result.Success(view);
    }
}
