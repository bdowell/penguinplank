using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media;

/// <summary>
/// The editable metadata of a <c>MediaAsset</c>: its caption, role, sort order, and visibility
/// (requirement 6.11 / A4). The storage key, MIME type, size, and checksum describe the stored
/// binary and are never editable through this request.
/// </summary>
/// <remarks>
/// A metadata edit is an ordinary value object with no business decision of its own; the use case
/// loads the asset, applies these fields, and persists through <see cref="IMediaAssetStore"/>.
/// Setting <see cref="Visibility"/> to <see cref="MediaVisibility.PublicApproved"/> marks the asset
/// as eligible for a public projection but never grants anonymous download access — every download
/// is still authorized at the API boundary (requirement 6.11).
/// </remarks>
public sealed record UpdateMediaMetadataRequest
{
    /// <summary>The asset to edit.</summary>
    public required Guid MediaAssetId { get; init; }

    /// <summary>The new caption, or <see langword="null"/> to clear it.</summary>
    public string? Caption { get; init; }

    /// <summary>The new role, or <see langword="null"/> to clear it.</summary>
    public string? Role { get; init; }

    /// <summary>The new sort order within the asset's role or gallery.</summary>
    public required int SortOrder { get; init; }

    /// <summary>The new visibility. Public approval is metadata only (requirement 6.11).</summary>
    public required MediaVisibility Visibility { get; init; }
}
