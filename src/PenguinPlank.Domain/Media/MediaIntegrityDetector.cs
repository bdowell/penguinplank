namespace PenguinPlank.Domain.Media;

/// <summary>
/// Pure detection of missing and orphaned media (requirement 8.7 / A6). It receives the set of
/// storage keys referenced by metadata and the set of keys present in file storage, and reports
/// exactly the set differences: metadata keys with no file (missing) and file keys with no metadata
/// (orphans). Nothing else is reported.
/// </summary>
/// <remarks>
/// Keys are compared ordinally; the file store issues lowercase GUID tokens so no case folding is
/// needed. Duplicate inputs are collapsed and results are ordered ordinally for determinism. The
/// function performs no I/O and reads no clock, so it is testable with ordinary values
/// (coding-standards §1).
/// </remarks>
public static class MediaIntegrityDetector
{
    /// <summary>
    /// Compares metadata storage keys with stored file keys.
    /// </summary>
    /// <param name="metadataKeys">The storage keys referenced by <c>MediaAsset</c> metadata.</param>
    /// <param name="storedFileKeys">The keys of the files present in file storage.</param>
    /// <returns>The missing-file keys and the orphan-file keys.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
    public static MediaIntegrityReport Detect(
        IEnumerable<string> metadataKeys,
        IEnumerable<string> storedFileKeys)
    {
        ArgumentNullException.ThrowIfNull(metadataKeys);
        ArgumentNullException.ThrowIfNull(storedFileKeys);

        var metadata = new HashSet<string>(metadataKeys, StringComparer.Ordinal);
        var stored = new HashSet<string>(storedFileKeys, StringComparer.Ordinal);

        string[] missing = metadata.Where(key => !stored.Contains(key)).Order(StringComparer.Ordinal).ToArray();
        string[] orphans = stored.Where(key => !metadata.Contains(key)).Order(StringComparer.Ordinal).ToArray();

        return new MediaIntegrityReport(missing, orphans);
    }
}
