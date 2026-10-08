using PenguinPlank.Application.Abstractions;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Media.UseCases;

/// <summary>
/// Opens the binary content of a stored media asset for an <b>already-authenticated, authorized</b>
/// caller: it looks up the asset's metadata, then streams the content through
/// <see cref="IFileStore.OpenAsync"/> using the asset's randomized storage key (requirement 6.11 /
/// A4).
/// </summary>
/// <remarks>
/// <para>
/// Authentication and authorization are enforced at the API boundary before this use case runs; the
/// endpoint resolves the authenticated <see cref="ActorContext"/> and only then invokes the
/// download. The use case requires a non-system authenticated actor as a defence-in-depth guard, so
/// that an anonymous request can never reach the file store even if the route policy were
/// misconfigured. Being <c>PublicApproved</c> is metadata only and is deliberately <b>not</b>
/// consulted here as an access grant: every download requires an authenticated authorized caller,
/// so a public-approved asset is never anonymously downloadable (requirement 6.11).
/// </para>
/// <para>
/// The use case returns a typed <see cref="Result{T}"/>: a <see cref="MediaContent"/> handle the
/// caller must dispose on success, a validation failure when no asset matches the id, and the file
/// store's failure if the backing file is missing. The field-level Owner/Staff projection of
/// <em>metadata</em> responses is a separate seam added in task 9.4; this use case concerns the
/// binary download and its authentication guard only.
/// </para>
/// </remarks>
public sealed class DownloadMediaUseCase
{
    private readonly IMediaAssetStore _mediaAssetStore;
    private readonly IFileStore _fileStore;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="mediaAssetStore">The metadata persistence boundary used to resolve the storage key.</param>
    /// <param name="fileStore">The file-store boundary that opens the binary content.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public DownloadMediaUseCase(IMediaAssetStore mediaAssetStore, IFileStore fileStore)
    {
        ArgumentNullException.ThrowIfNull(mediaAssetStore);
        ArgumentNullException.ThrowIfNull(fileStore);

        _mediaAssetStore = mediaAssetStore;
        _fileStore = fileStore;
    }

    /// <summary>Executes the authorized download.</summary>
    /// <param name="mediaAssetId">The asset to download.</param>
    /// <param name="actor">
    /// The authenticated actor resolved at the API boundary. An anonymous caller never reaches this
    /// use case; a system-worker actor is not a download client and is refused.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying a readable <see cref="MediaContent"/> the caller must dispose; a
    /// <see cref="ErrorCode.Forbidden"/> failure when the actor is not an authorized download
    /// client; a <see cref="ErrorCode.Validation"/> failure when no asset matches the id; or the
    /// file store's failure when the backing file is missing.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<MediaContent>> ExecuteAsync(
        Guid mediaAssetId,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // Defence in depth: a download requires an authenticated interactive actor. Public-approved
        // visibility is never consulted as an access grant (requirement 6.11).
        if (actor.IsSystemWorker)
        {
            return Result.Failure<MediaContent>(
                ErrorCode.Forbidden,
                "Media downloads require an authenticated interactive actor.");
        }

        MediaAssetView? view = await _mediaAssetStore
            .GetAsync(mediaAssetId, cancellationToken)
            .ConfigureAwait(false);

        if (view is null)
        {
            return Result.Failure<MediaContent>(ErrorCode.Validation, "No media asset matches the supplied id.");
        }

        return await _fileStore
            .OpenAsync(new StorageKey(view.StorageKey), cancellationToken)
            .ConfigureAwait(false);
    }
}
