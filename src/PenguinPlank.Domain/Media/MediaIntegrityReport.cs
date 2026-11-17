namespace PenguinPlank.Domain.Media;

/// <summary>
/// The outcome of comparing media metadata against stored files (requirement 8.7 / A6): metadata
/// whose backing file is missing, and stored files that no metadata references.
/// </summary>
/// <param name="MissingFileKeys">
/// Storage keys referenced by <c>MediaAsset</c> metadata for which no stored file exists, ordered
/// ordinally and without duplicates.
/// </param>
/// <param name="OrphanFileKeys">
/// Storage keys of stored files that no <c>MediaAsset</c> metadata references, ordered ordinally and
/// without duplicates.
/// </param>
public sealed record MediaIntegrityReport(
    IReadOnlyList<string> MissingFileKeys,
    IReadOnlyList<string> OrphanFileKeys)
{
    /// <summary>Gets a value indicating whether neither a missing file nor an orphan was found.</summary>
    public bool IsClean => MissingFileKeys.Count == 0 && OrphanFileKeys.Count == 0;
}
