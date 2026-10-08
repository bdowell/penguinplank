using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media;

/// <summary>
/// The decided metadata the upload use case hands to <see cref="IMediaAssetStore"/> to persist a
/// new <c>MediaAsset</c> after the binary content has been validated and stored by the file store
/// (requirements 6.8, 6.9 / A4).
/// </summary>
/// <remarks>
/// Every field is already decided by the use case: the identifier comes from the injected
/// <c>IIdentifierGenerator</c>, the timestamps from the injected <c>TimeProvider</c>, and the
/// storage key, MIME type, size, and checksum from the file store's <c>StoredMedia</c> facts. The
/// store performs no business decision; it only maps these values onto the Domain entity and
/// inserts it (coding-standards §1, §3). A new asset always starts
/// <see cref="MediaVisibility.Private"/> — public approval is a later, explicit metadata edit and
/// never happens on upload (requirement 6.11).
/// </remarks>
public sealed record NewMediaAssetRecord
{
    /// <summary>The identifier assigned to the new asset.</summary>
    public required Guid MediaAssetId { get; init; }

    /// <summary>The randomized storage key the file store returned.</summary>
    public required string StorageKey { get; init; }

    /// <summary>The allowlist-validated MIME type of the stored content.</summary>
    public required string MimeType { get; init; }

    /// <summary>The actual number of bytes written to the store.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>A checksum of the stored content.</summary>
    public required string Checksum { get; init; }

    /// <summary>An optional human-readable caption retained from the upload.</summary>
    public string? Caption { get; init; }

    /// <summary>The visibility the asset starts with — always <see cref="MediaVisibility.Private"/> on upload.</summary>
    public required MediaVisibility Visibility { get; init; }

    /// <summary>The creation/modification instant stamped from the injected clock.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
