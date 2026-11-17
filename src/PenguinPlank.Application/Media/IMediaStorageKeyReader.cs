namespace PenguinPlank.Application.Media;

/// <summary>
/// Narrow read boundary returning the storage key of every <c>MediaAsset</c> metadata row, used to
/// detect missing and orphaned media (requirement 8.7 / A6).
/// </summary>
public interface IMediaStorageKeyReader
{
    /// <summary>Reads the storage key of every media-asset metadata row.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The storage keys referenced by metadata.</returns>
    Task<IReadOnlyList<string>> GetAllStorageKeysAsync(CancellationToken cancellationToken);
}
