namespace PenguinPlank.Contracts.Media;

/// <summary>
/// The versioned transport shape returned for a single <c>MediaAsset</c>'s metadata on an upload,
/// read, or edit (requirements 6.8–6.11 / A4).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO with no persistence entity or EF type (dependency rule;
/// coding-standards §3) — the API maps the Application <c>MediaAssetView</c> read model into this
/// shape. It deliberately omits the randomized storage key: the key is an internal, non-guessable
/// token (requirement 6.9) and the client references the asset by its id. <see cref="Visibility"/>
/// is a stable string ("Private" or "PublicApproved") describing eligibility for a public
/// projection only; it never grants anonymous download access — every download is authorized at
/// the API boundary (requirement 6.11). The field-level Owner/Staff projection of this response is
/// the separate seam added in task 9.4.
/// </remarks>
public sealed record MediaAssetResponse
{
    /// <summary>The asset's stable, immutable identifier.</summary>
    public required Guid MediaAssetId { get; init; }

    /// <summary>The allowlist-validated MIME type of the stored content.</summary>
    public required string MimeType { get; init; }

    /// <summary>The size of the stored content in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>A checksum of the stored content.</summary>
    public required string Checksum { get; init; }

    /// <summary>An optional human-readable caption.</summary>
    public string? Caption { get; init; }

    /// <summary>The optional role the asset plays within its owner's gallery.</summary>
    public string? Role { get; init; }

    /// <summary>The sort order of the asset within its role or gallery.</summary>
    public required int SortOrder { get; init; }

    /// <summary>The asset's visibility as a stable string ("Private" or "PublicApproved").</summary>
    public required string Visibility { get; init; }

    /// <summary>The instant the asset's metadata was first persisted, with offset.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The instant the asset's metadata was last modified, with offset.</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
