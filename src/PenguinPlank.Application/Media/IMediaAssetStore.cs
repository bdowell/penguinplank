namespace PenguinPlank.Application.Media;

/// <summary>
/// The narrow persistence boundary for <c>MediaAsset</c> <b>metadata</b> rows, separate from the
/// <see cref="IFileStore"/> that holds the binary content outside the database and web root
/// (requirements 6.8–6.11 / A4).
/// </summary>
/// <remarks>
/// <para>
/// This is a focused, responsibility-named interface defined in Application and implemented in
/// Infrastructure over EF Core (coding-standards §2, §3). It is deliberately small — insert a
/// decided record, read a view by id, update the editable metadata — so the media use cases can be
/// tested against a controllable substitute without a live database. It maps Domain entities to the
/// Application <see cref="MediaAssetView"/> read model; no EF entity, <c>DbContext</c>, or
/// <c>IQueryable</c> crosses the boundary (coding-standards §3).
/// </para>
/// <para>
/// The store performs no business or authorization decision. Authorization for a download is
/// enforced at the API boundary before the download use case reads a view; the store simply returns
/// the metadata, including the storage key the file store needs (requirement 6.11). Every method
/// takes and propagates a <see cref="CancellationToken"/>.
/// </para>
/// </remarks>
public interface IMediaAssetStore
{
    /// <summary>
    /// Persists a decided new media-asset metadata record.
    /// </summary>
    /// <param name="record">The fully decided metadata to insert.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the record is persisted.</returns>
    Task InsertAsync(NewMediaAssetRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the metadata view for a single asset.
    /// </summary>
    /// <param name="mediaAssetId">The asset's identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The asset's view, or <see langword="null"/> when no asset matches the id.</returns>
    Task<MediaAssetView?> GetAsync(Guid mediaAssetId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the editable metadata (caption, role, sort order, visibility) to an existing asset
    /// and persists it, stamping the supplied modification instant.
    /// </summary>
    /// <param name="request">The editable metadata to apply.</param>
    /// <param name="updatedAtUtc">The modification instant supplied from an injected clock.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The updated view, or <see langword="null"/> when no asset matches
    /// <see cref="UpdateMediaMetadataRequest.MediaAssetId"/>.
    /// </returns>
    Task<MediaAssetView?> UpdateMetadataAsync(
        UpdateMediaMetadataRequest request,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}
