namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned transport shape returned for one immutable <c>CareProfileVersion</c>.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO with no persistence entity or EF type (dependency rule;
/// coding-standards §3) — the API maps the Application <c>CareProfileVersionView</c> read model
/// into this shape. Care-profile versioning is append-only: each care edit creates a new version
/// and prior versions are never modified or deleted (requirements 2.3, 2.5). The
/// <see cref="CreatedAt"/> instant carries its offset (requirement A8 §10.3).
/// </remarks>
public sealed record CareProfileVersionResponse
{
    /// <summary>The version's stable identifier.</summary>
    public required Guid CareProfileVersionId { get; init; }

    /// <summary>The owning care profile's identifier.</summary>
    public required Guid CareProfileId { get; init; }

    /// <summary>The monotonically increasing version number within the profile (the first version is 1).</summary>
    public required int VersionNumber { get; init; }

    /// <summary>The care guidance text captured by this immutable version.</summary>
    public required string Guidance { get; init; }

    /// <summary>The instant this version was created, with offset.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
