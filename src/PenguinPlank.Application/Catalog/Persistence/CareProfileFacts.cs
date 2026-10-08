namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The existing-state facts an add-care-version use case needs about a <c>CareProfile</c> before
/// it appends the next immutable version.
/// </summary>
/// <remarks>
/// A use case loads this snapshot through <see cref="ICareProfileVersionStore"/> and feeds
/// <see cref="CurrentMaxVersionNumber"/> to the pure <c>CareVersionResolver.NextVersionNumber</c>
/// decision before appending (coding-standards §1). Care-profile versioning is append-only: the use
/// case never updates or deletes an existing version (requirements 2.3, 2.5). This is an immutable
/// value with no I/O (coding-standards §1).
/// </remarks>
public sealed record CareProfileFacts
{
    /// <summary>Whether the profile is active; an archived profile rejects new versions (requirement 1.8).</summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// The highest version number that currently exists for the profile, or <c>0</c> when the
    /// profile has no versions yet. Fed to <c>CareVersionResolver.NextVersionNumber</c>.
    /// </summary>
    public required int CurrentMaxVersionNumber { get; init; }
}
