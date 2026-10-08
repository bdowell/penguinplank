namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned request body for appending a new immutable version to a <c>CareProfile</c> via
/// <c>POST /api/v1/care-profiles/{id}/versions</c>.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO carrying intent only (dependency rule; coding-standards §3).
/// Care-profile versioning is append-only: this never mutates an existing version but adds the
/// next one, and the server assigns the version number and creation instant (requirements 2.3,
/// 2.5).
/// </remarks>
public sealed record AddCareVersionContract
{
    /// <summary>The care guidance text for the new immutable version. Required and non-empty.</summary>
    public required string Guidance { get; init; }
}
