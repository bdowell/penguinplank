using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media;

/// <summary>
/// The read model describing a stored <c>MediaAsset</c>'s metadata, returned by the media use
/// cases and projected onto a Contracts response at the API boundary (requirements 6.8–6.11 / A4).
/// </summary>
/// <remarks>
/// <para>
/// This is a plain Application read model: it carries ordinary values mapped from the Domain
/// <c>MediaAsset</c> by the <see cref="IMediaAssetStore"/> adapter, so no EF entity, <c>DbContext</c>,
/// or <c>IQueryable</c> crosses the boundary into the API or Domain decision code
/// (coding-standards §2, §3). It is directly constructible in a test without starting the
/// application.
/// </para>
/// <para>
/// It intentionally includes the <see cref="StorageKey"/> because the download use case needs it to
/// open the binary through the file store once the caller is authorized; the API never returns the
/// storage key to a client in a metadata response, and the key is a randomized token that is not
/// guessable and never derived from the original filename (requirement 6.9).
/// </para>
/// </remarks>
public sealed record MediaAssetView
{
    /// <summary>The asset's stable, immutable identifier.</summary>
    public required Guid MediaAssetId { get; init; }

    /// <summary>The randomized storage key under which the binary content is held by the file store.</summary>
    public required string StorageKey { get; init; }

    /// <summary>The allowlist-validated MIME type of the stored content.</summary>
    public required string MimeType { get; init; }

    /// <summary>The size of the stored content in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>A checksum of the stored content.</summary>
    public required string Checksum { get; init; }

    /// <summary>An optional human-readable caption for the asset.</summary>
    public string? Caption { get; init; }

    /// <summary>The optional role the asset plays within its owner's gallery (for example "Primary").</summary>
    public string? Role { get; init; }

    /// <summary>The sort order of the asset within its role or gallery.</summary>
    public required int SortOrder { get; init; }

    /// <summary>
    /// The asset's visibility. Being <see cref="MediaVisibility.PublicApproved"/> is metadata only
    /// and never grants anonymous download access (requirement 6.11).
    /// </summary>
    public required MediaVisibility Visibility { get; init; }

    /// <summary>The instant the asset's metadata was first persisted.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The instant the asset's metadata was last modified.</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
