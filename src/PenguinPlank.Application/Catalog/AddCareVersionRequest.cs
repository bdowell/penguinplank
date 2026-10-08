namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to append a new immutable version to an existing <c>CareProfile</c>.
/// </summary>
/// <remarks>
/// Care-profile versioning is append-only: editing guidance never mutates an existing version but
/// adds the next one (requirements 2.3, 2.5). This immutable request names the profile and carries
/// the new guidance text; the store assigns the next version number and creation instant. It
/// performs no I/O and is directly testable (coding-standards §1).
/// </remarks>
public sealed record AddCareVersionRequest
{
    /// <summary>Creates a request to append a care-profile version.</summary>
    /// <param name="careProfileId">The owning care profile; must be non-empty.</param>
    /// <param name="guidance">The care guidance text for the new version; must be non-empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="careProfileId"/> is empty or <paramref name="guidance"/> is null or whitespace.</exception>
    public AddCareVersionRequest(Guid careProfileId, string guidance)
    {
        if (careProfileId == Guid.Empty)
        {
            throw new ArgumentException("A care version requires a non-empty care profile id.", nameof(careProfileId));
        }

        if (string.IsNullOrWhiteSpace(guidance))
        {
            throw new ArgumentException("A care version requires non-empty guidance.", nameof(guidance));
        }

        CareProfileId = careProfileId;
        Guidance = guidance;
    }

    /// <summary>The owning care profile.</summary>
    public Guid CareProfileId { get; }

    /// <summary>The care guidance text captured by the new immutable version.</summary>
    public string Guidance { get; }
}
