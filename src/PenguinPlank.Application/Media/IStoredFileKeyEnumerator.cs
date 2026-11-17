namespace PenguinPlank.Application.Media;

/// <summary>
/// Narrow boundary enumerating the keys of every file present in media file storage, used to detect
/// missing and orphaned media (requirement 8.7 / A6). Kept separate from <see cref="IFileStore"/> so
/// the save/open/delete contract stays small.
/// </summary>
public interface IStoredFileKeyEnumerator
{
    /// <summary>Lists the key of every stored media file.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The keys of the stored files; empty when storage holds no files.</returns>
    Task<IReadOnlyList<string>> ListStoredKeysAsync(CancellationToken cancellationToken);
}
