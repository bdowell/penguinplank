namespace PenguinPlank.Application.Catalog;

/// <summary>
/// A read model describing one immutable <c>CareProfileVersion</c>.
/// </summary>
/// <remarks>
/// Care-profile versioning is append-only: each care edit creates a new version and prior
/// versions are never modified or deleted (requirements 2.3, 2.5). This flat, immutable snapshot
/// is returned by the care-profile store instead of the EF entity so no persistence type crosses
/// the Application boundary (coding-standards §2, §3). A produced piece resolves the version in
/// effect at its production time and keeps resolving it thereafter (requirement 2.4).
/// </remarks>
public sealed record CareProfileVersionView
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
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
