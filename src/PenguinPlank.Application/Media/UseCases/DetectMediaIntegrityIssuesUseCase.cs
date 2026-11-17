using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media.UseCases;

/// <summary>
/// Reports media metadata whose backing file is missing and stored files with no referencing
/// metadata (requirement 8.7 / A6). Read-only: it never deletes or repairs anything.
/// </summary>
/// <remarks>
/// Orchestrates two narrow readers and delegates the decision to the pure
/// <see cref="MediaIntegrityDetector"/>.
/// </remarks>
public sealed class DetectMediaIntegrityIssuesUseCase
{
    private readonly IMediaStorageKeyReader _metadataKeyReader;
    private readonly IStoredFileKeyEnumerator _fileKeyEnumerator;

    /// <summary>Creates the use case.</summary>
    /// <param name="metadataKeyReader">Reads storage keys referenced by metadata.</param>
    /// <param name="fileKeyEnumerator">Lists keys of stored files.</param>
    /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
    public DetectMediaIntegrityIssuesUseCase(
        IMediaStorageKeyReader metadataKeyReader,
        IStoredFileKeyEnumerator fileKeyEnumerator)
    {
        ArgumentNullException.ThrowIfNull(metadataKeyReader);
        ArgumentNullException.ThrowIfNull(fileKeyEnumerator);

        _metadataKeyReader = metadataKeyReader;
        _fileKeyEnumerator = fileKeyEnumerator;
    }

    /// <summary>Detects missing and orphaned media.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The integrity report.</returns>
    public async Task<MediaIntegrityReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> metadataKeys = await _metadataKeyReader
            .GetAllStorageKeysAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> storedKeys = await _fileKeyEnumerator
            .ListStoredKeysAsync(cancellationToken)
            .ConfigureAwait(false);

        return MediaIntegrityDetector.Detect(metadataKeys, storedKeys);
    }
}
