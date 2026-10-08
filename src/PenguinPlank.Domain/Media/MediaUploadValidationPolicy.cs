namespace PenguinPlank.Domain.Media;

/// <summary>
/// The pure business policy that decides whether a media upload is acceptable (requirements 6.9,
/// 6.10 / A4).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Validate"/> is a pure function: it receives the claimed content type, the declared
/// size, and the file's leading bytes together with the applicable <see cref="MediaSizeLimits"/>,
/// and returns an accept/reject decision. It performs no I/O, reads no clock, touches no file store,
/// and needs no application startup, so the property test (task 6.10, Property 17) and unit tests
/// can exercise it directly (coding-standards §1). The <c>IFileStore</c> adapter and the media
/// upload use case call this policy and then orchestrate persistence; the decision never lives
/// inside a side-effecting method.
/// </para>
/// <para>
/// The checks, in order, are:
/// </para>
/// <list type="number">
///   <item><description>Reject empty content or a non-positive declared size.</description></item>
///   <item><description>Reject the leading bytes if they match a known executable or HTML/script
///   signature — this guard runs <em>before</em> the allowlist so a dangerous payload cannot slip
///   through by also matching an allowlisted prefix (requirement 6.9).</description></item>
///   <item><description>Reject a content type that is not on the allowlist (requirement 6.9).</description></item>
///   <item><description>Reject when the leading bytes do not match the magic-byte signature of the
///   claimed content type (requirement 6.9).</description></item>
///   <item><description>Reject when the declared size exceeds the category bound — 20 MB image /
///   200 MB video from the supplied limits (requirement 6.10).</description></item>
/// </list>
/// <para>
/// The allowlist (<c>image/jpeg</c>, <c>image/png</c>, <c>image/webp</c>, <c>image/gif</c>,
/// <c>video/mp4</c>, <c>video/quicktime</c>) and the executable/HTML reject signatures are declared
/// here as named static data so there are no scattered magic values (coding-standards §5).
/// </para>
/// </remarks>
public static class MediaUploadValidationPolicy
{
    /// <summary>
    /// The number of leading bytes a caller should supply for signature inspection. Enough to cover
    /// every allowlist and reject signature (the longest marker sits a few bytes in).
    /// </summary>
    public const int SignatureProbeLength = 16;

    // Allowlisted content types and the magic-byte signatures their leading bytes must match
    // (requirement 6.9). JPEG: FF D8 FF. PNG: 89 50 4E 47 0D 0A 1A 0A. GIF: "GIF87a"/"GIF89a".
    // WebP: "RIFF" at 0 then "WEBP" at 8. MP4/QuickTime: "ftyp" box tag at offset 4.
    private static readonly IReadOnlyList<MediaContentSignature> s_allowlist = new[]
    {
        new MediaContentSignature(
            "image/jpeg",
            MediaCategory.Image,
            new MagicByteSignature(0, 0xFF, 0xD8, 0xFF)),
        new MediaContentSignature(
            "image/png",
            MediaCategory.Image,
            new MagicByteSignature(0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)),
        new MediaContentSignature(
            "image/gif",
            MediaCategory.Image,
            new MagicByteSignature(Ascii("GIF87a")),
            new MagicByteSignature(Ascii("GIF89a"))),
        new MediaContentSignature(
            "image/webp",
            MediaCategory.Image,
            new MagicByteSignature(8, Ascii("WEBP"))),
        new MediaContentSignature(
            "video/mp4",
            MediaCategory.Video,
            new MagicByteSignature(4, Ascii("ftyp"))),
        new MediaContentSignature(
            "video/quicktime",
            MediaCategory.Video,
            new MagicByteSignature(4, Ascii("ftyp")),
            new MagicByteSignature(4, Ascii("moov"))),
    };

    // Signatures that are rejected outright, no matter what content type is claimed (requirement
    // 6.9: reject executable/HTML uploads). HTML/script is matched case-insensitively via explicit
    // upper/lower variants of the opening markers.
    private static readonly List<MagicByteSignature> s_dangerousSignatures = BuildDangerousSignatures();

    /// <summary>The allowlisted MIME types, for diagnostics and configuration surfaces.</summary>
    public static IReadOnlyCollection<string> AllowedContentTypes =>
        s_allowlist.Select(entry => entry.MimeType).ToArray();

    /// <summary>
    /// Validates a media upload against the allowlist, magic-byte signatures, dangerous-content
    /// guard, and size bounds.
    /// </summary>
    /// <param name="claimedContentType">The content type the caller claims for the file.</param>
    /// <param name="declaredSizeBytes">The declared size of the complete file, in bytes.</param>
    /// <param name="leadingBytes">
    /// The file's leading bytes for signature inspection. Supply at least
    /// <see cref="SignatureProbeLength"/> bytes (or the whole file when it is smaller).
    /// </param>
    /// <param name="limits">The size bounds that apply (from validated typed options).</param>
    /// <returns>An accept decision carrying the resolved category, or a specific rejection.</returns>
    public static MediaUploadValidationResult Validate(
        string? claimedContentType,
        long declaredSizeBytes,
        ReadOnlySpan<byte> leadingBytes,
        MediaSizeLimits limits)
    {
        // 1. Empty or non-positive declared size.
        if (declaredSizeBytes <= 0 || leadingBytes.Length == 0)
        {
            return MediaUploadValidationResult.Reject(
                MediaUploadRejectionReason.EmptyContent,
                "The upload has no content.");
        }

        // 2. Dangerous content: reject executable/HTML/script signatures before anything else.
        foreach (MagicByteSignature dangerous in s_dangerousSignatures)
        {
            if (dangerous.Matches(leadingBytes))
            {
                return MediaUploadValidationResult.Reject(
                    MediaUploadRejectionReason.DangerousContent,
                    "The upload is an executable or HTML/script file, which is not allowed.");
            }
        }

        // 3. Content-type allowlist.
        MediaContentSignature? entry = s_allowlist.FirstOrDefault(
            candidate => string.Equals(candidate.MimeType, Normalize(claimedContentType), StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return MediaUploadValidationResult.Reject(
                MediaUploadRejectionReason.DisallowedContentType,
                "The content type is not an allowed image or video type.");
        }

        // 4. Magic-byte signature match for the claimed type.
        if (!entry.Matches(leadingBytes))
        {
            return MediaUploadValidationResult.Reject(
                MediaUploadRejectionReason.SignatureMismatch,
                "The file content does not match its declared content type.");
        }

        // 5. Size bound for the resolved category.
        long maxBytes = limits.MaxBytesFor(entry.Category);
        if (declaredSizeBytes > maxBytes)
        {
            return MediaUploadValidationResult.Reject(
                MediaUploadRejectionReason.SizeExceeded,
                "The upload exceeds the maximum allowed size for its media type.");
        }

        return MediaUploadValidationResult.Accept(entry.Category);
    }

    private static string Normalize(string? contentType) =>
        contentType?.Trim() ?? string.Empty;

    private static byte[] Ascii(string text) =>
        System.Text.Encoding.ASCII.GetBytes(text);

    private static List<MagicByteSignature> BuildDangerousSignatures()
    {
        var signatures = new List<MagicByteSignature>
        {
            // Windows PE executable ("MZ") and ELF binaries (0x7F "ELF").
            new MagicByteSignature(Ascii("MZ")),
            new MagicByteSignature(0, 0x7F, 0x45, 0x4C, 0x46),

            // Shebang scripts ("#!").
            new MagicByteSignature(Ascii("#!")),
        };

        // HTML/script markers, matched case-insensitively at offset 0: "<!DOCTYPE", "<html",
        // "<?xml", "<script", "<svg". Each case variant is a distinct exact signature.
        foreach (string marker in new[] { "<!DOCTYPE", "<html", "<?xml", "<script", "<svg", "<HTML", "<SCRIPT", "<SVG" })
        {
            signatures.Add(new MagicByteSignature(Ascii(marker)));
        }

        return signatures;
    }
}
