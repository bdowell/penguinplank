using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Media;

/// <summary>
/// The metadata record for a stored media file: its randomized storage key, MIME type, size,
/// checksum, caption, role, sort order, and visibility. The binary content lives outside the
/// relational database and outside the web root.
/// </summary>
/// <remarks>
/// <para>
/// Media binary content is stored through the <c>IFileStore</c> abstraction outside the web root
/// (requirement 6.8 / A4); this entity holds only the metadata. <see cref="StorageKey"/> is a
/// randomized, unique key assigned on upload (requirement 6.9) so a file's location is not
/// guessable and does not leak the original filename. <see cref="Checksum"/> and
/// <see cref="SizeBytes"/> record the validated content; <see cref="MimeType"/> is the
/// allowlist-validated type. <see cref="Visibility"/> is metadata only and never grants anonymous
/// access (requirement 6.11).
/// </para>
/// <para>
/// Associations to catalog records are made exclusively through the explicit foreign-key join
/// tables <see cref="ProductMedia"/>, <see cref="VariantMedia"/>, and <see cref="PieceMedia"/> —
/// never a polymorphic <c>EntityType</c>/<c>EntityId</c> link (requirement 6.12 / design
/// invariant 9).
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: an asset's caption/role/visibility may be edited, but it
/// is a lightweight metadata record rather than a concurrency-protected aggregate. Upload
/// validation (allowlist, signature, size bounds) is an <c>IFileStore</c> concern added by a
/// later task.
/// </para>
/// </remarks>
public class MediaAsset : Entity
{
    /// <summary>
    /// The randomized, unique storage key under which the binary content is held by the file
    /// store. Not guessable and not derived from the original filename (requirement 6.9).
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>The allowlist-validated MIME type of the stored content.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>The size of the stored content in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>A checksum of the stored content, used to detect corruption or mismatch.</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>An optional human-readable caption for the asset.</summary>
    public string? Caption { get; set; }

    /// <summary>The role the asset plays (for example, "Primary", "Gallery", "Thumbnail").</summary>
    public string? Role { get; set; }

    /// <summary>The sort order of the asset within its role or gallery.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// The asset's visibility. Defaults to <see cref="MediaVisibility.Private"/>; public approval
    /// is metadata only and never grants anonymous download access (requirement 6.11).
    /// </summary>
    public MediaVisibility Visibility { get; set; } = MediaVisibility.Private;
}
