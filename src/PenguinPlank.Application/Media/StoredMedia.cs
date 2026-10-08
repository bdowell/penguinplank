using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media;

/// <summary>
/// The result of persisting a validated media upload: the randomized <see cref="StorageKey"/> under
/// which the content now lives, together with the facts the metadata record needs (requirements
/// 6.8, 6.9 / A4).
/// </summary>
/// <remarks>
/// The caller (the media upload use case) builds the <c>MediaAsset</c> metadata from these values
/// — the storage key, the validated content type, the actual stored byte count, the resolved
/// category, and a content checksum — then persists the metadata relationally while the binary
/// stays outside the database and web root.
/// </remarks>
public sealed class StoredMedia
{
    /// <summary>
    /// Creates the stored-media descriptor.
    /// </summary>
    /// <param name="storageKey">The randomized key the content was stored under.</param>
    /// <param name="contentType">The allowlist-validated content type.</param>
    /// <param name="sizeBytes">The actual number of bytes written.</param>
    /// <param name="category">The resolved media category.</param>
    /// <param name="checksum">A hex checksum of the stored content.</param>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="contentType"/> or <paramref name="checksum"/> is blank.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="sizeBytes"/> is not positive.</exception>
    public StoredMedia(StorageKey storageKey, string contentType, long sizeBytes, MediaCategory category, string checksum)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new System.ArgumentException("Stored media requires a content type.", nameof(contentType));
        }

        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new System.ArgumentException("Stored media requires a checksum.", nameof(checksum));
        }

        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        StorageKey = storageKey;
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        Category = category;
        Checksum = checksum.Trim();
    }

    /// <summary>The randomized key the content is stored under.</summary>
    public StorageKey StorageKey { get; }

    /// <summary>The allowlist-validated content type.</summary>
    public string ContentType { get; }

    /// <summary>The actual number of bytes written to the store.</summary>
    public long SizeBytes { get; }

    /// <summary>The resolved media category.</summary>
    public MediaCategory Category { get; }

    /// <summary>A hex checksum of the stored content.</summary>
    public string Checksum { get; }
}
