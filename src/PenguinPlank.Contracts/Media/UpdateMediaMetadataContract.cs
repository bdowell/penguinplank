namespace PenguinPlank.Contracts.Media;

/// <summary>
/// The versioned request body for editing a <c>MediaAsset</c>'s editable metadata via
/// <c>PATCH /api/v1/media/{id}</c> (requirement 6.11 / A4).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO carrying intent only (dependency rule; coding-standards §3). Only the
/// caption, role, sort order, and visibility are editable; the storage key, MIME type, size, and
/// checksum describe the stored binary and are never changed. Setting <see cref="Visibility"/> to
/// "PublicApproved" marks the asset as eligible for a public projection — it never grants anonymous
/// download access (requirement 6.11).
/// </remarks>
public sealed record UpdateMediaMetadataContract
{
    /// <summary>The new caption, or <see langword="null"/> to clear it.</summary>
    public string? Caption { get; init; }

    /// <summary>The new role, or <see langword="null"/> to clear it.</summary>
    public string? Role { get; init; }

    /// <summary>The new sort order within the asset's role or gallery.</summary>
    public required int SortOrder { get; init; }

    /// <summary>The new visibility as a stable string ("Private" or "PublicApproved").</summary>
    public required string Visibility { get; init; }
}
