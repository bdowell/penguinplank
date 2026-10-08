namespace PenguinPlank.Domain.Media;

/// <summary>
/// One allowlisted media content type paired with the magic-byte signatures that a file's leading
/// bytes must match for the claimed type to be accepted (requirement 6.9 / A4).
/// </summary>
/// <remarks>
/// <para>
/// A signature is a short, position-anchored sequence of leading bytes. A content type may have
/// more than one valid signature (for example WebP and MP4 both have a fixed prefix and a marker a
/// few bytes later, and GIF has two version strings), so each entry carries a set of acceptable
/// signatures and a leading-byte match against <em>any</em> of them accepts the type.
/// </para>
/// <para>
/// This type is pure data consumed by <see cref="MediaUploadValidationPolicy"/>. It performs no
/// I/O and holds no mutable state.
/// </para>
/// </remarks>
public sealed class MediaContentSignature
{
    private readonly IReadOnlyList<MagicByteSignature> _signatures;

    /// <summary>
    /// Creates an allowlist entry.
    /// </summary>
    /// <param name="mimeType">The allowlisted MIME type (for example <c>image/png</c>).</param>
    /// <param name="category">The category that selects this type's size bound.</param>
    /// <param name="signatures">The acceptable leading-byte signatures; at least one is required.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="mimeType"/> is null/blank or <paramref name="signatures"/> is empty.
    /// </exception>
    public MediaContentSignature(string mimeType, MediaCategory category, params MagicByteSignature[] signatures)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new System.ArgumentException("A content signature requires a non-empty MIME type.", nameof(mimeType));
        }

        System.ArgumentNullException.ThrowIfNull(signatures);

        if (signatures.Length == 0)
        {
            throw new System.ArgumentException("A content signature requires at least one magic-byte signature.", nameof(signatures));
        }

        MimeType = mimeType;
        Category = category;
        _signatures = signatures.ToArray();
    }

    /// <summary>The allowlisted MIME type.</summary>
    public string MimeType { get; }

    /// <summary>The category that selects the applicable size bound.</summary>
    public MediaCategory Category { get; }

    /// <summary>
    /// Determines whether the given leading bytes match any of this type's accepted signatures.
    /// </summary>
    /// <param name="leadingBytes">The file's leading bytes.</param>
    /// <returns><see langword="true"/> when at least one signature matches.</returns>
    public bool Matches(System.ReadOnlySpan<byte> leadingBytes)
    {
        foreach (MagicByteSignature signature in _signatures)
        {
            if (signature.Matches(leadingBytes))
            {
                return true;
            }
        }

        return false;
    }
}
