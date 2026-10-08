namespace PenguinPlank.Application.Media.UseCases;

/// <summary>
/// Reads a stored media asset's metadata view by id (requirement 6.8 / A4). This is the read side a
/// thin metadata endpoint maps onto; it holds the lookup orchestration out of the API layer
/// (coding-standards §3).
/// </summary>
/// <remarks>
/// The use case is a thin pass-through over <see cref="IMediaAssetStore.GetAsync"/>, returning the
/// Application <see cref="MediaAssetView"/> read model (never an EF entity) or
/// <see langword="null"/> when no asset matches. Authentication is enforced at the API boundary; the
/// field-level Owner/Staff projection of the metadata response is the separate seam added in task
/// 9.4.
/// </remarks>
public sealed class GetMediaMetadataUseCase
{
    private readonly IMediaAssetStore _mediaAssetStore;

    /// <summary>Creates the use case with its injected boundary.</summary>
    /// <param name="mediaAssetStore">The metadata persistence boundary.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaAssetStore"/> is <see langword="null"/>.</exception>
    public GetMediaMetadataUseCase(IMediaAssetStore mediaAssetStore)
    {
        ArgumentNullException.ThrowIfNull(mediaAssetStore);

        _mediaAssetStore = mediaAssetStore;
    }

    /// <summary>Reads the metadata view for a single asset.</summary>
    /// <param name="mediaAssetId">The asset's identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The asset's view, or <see langword="null"/> when no asset matches the id.</returns>
    public Task<MediaAssetView?> ExecuteAsync(Guid mediaAssetId, CancellationToken cancellationToken) =>
        _mediaAssetStore.GetAsync(mediaAssetId, cancellationToken);
}
