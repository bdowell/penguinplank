using System.Text;

using CsCheck;

using PenguinPlank.Domain.Media;

namespace UnitTests.Media;

/// <summary>
/// Property 17 (requirements A4 Â§6.9, Â§6.10): <em>Media upload validation.</em> For the pure
/// <see cref="MediaUploadValidationPolicy.Validate"/> policy the following hold across randomly
/// generated inputs:
/// <list type="number">
///   <item><description>A valid upload â€” an allowlisted content type whose leading bytes carry the
///   correct magic-byte signature and whose declared size is within the category bound â€” is
///   accepted and resolves to the right <see cref="MediaCategory"/> (Â§6.9/Â§6.10).</description></item>
///   <item><description>A valid-signature upload whose declared size exceeds the category bound is
///   rejected with <see cref="MediaUploadRejectionReason.SizeExceeded"/> (Â§6.10).</description></item>
///   <item><description>An allowlisted content type whose leading bytes do not match its signature
///   is rejected with <see cref="MediaUploadRejectionReason.SignatureMismatch"/> (Â§6.9).</description></item>
///   <item><description>A content type that is not on the allowlist is rejected with
///   <see cref="MediaUploadRejectionReason.DisallowedContentType"/> (Â§6.9).</description></item>
///   <item><description>Leading bytes that match an executable or HTML/script signature are
///   rejected with <see cref="MediaUploadRejectionReason.DangerousContent"/> even when the claimed
///   content type is allowlisted (Â§6.9).</description></item>
///   <item><description>An empty upload is rejected with
///   <see cref="MediaUploadRejectionReason.EmptyContent"/>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// The policy is a pure function (coding-standards Â§1): the test constructs explicit inputs and
/// asserts the returned <see cref="MediaUploadValidationResult"/> with no database, file system,
/// clock, or application startup. Generators are built from the <em>authoritative</em> signatures
/// declared in <see cref="MediaUploadValidationPolicy"/> so a generated "correct" prefix actually
/// matches, and a generated "wrong" prefix provably does not. CsCheck (pinned in
/// <c>UnitTests.csproj</c>) runs each property well above the design's â‰¥100-iteration floor. The
/// companion example-based coverage of individual reasons is intentionally omitted here; this file
/// adds only Property 17.
///
/// **Validates: Requirements 6.9, 6.10**
/// </remarks>
public class MediaUploadValidationPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    private static readonly MediaSizeLimits s_limits = MediaSizeLimits.Default;

    /// <summary>
    /// The authoritative allowlist mirrored from <see cref="MediaUploadValidationPolicy"/>: each
    /// MIME type, its resolved category, and a factory that produces a leading-byte prefix carrying
    /// a <em>correct</em> magic-byte signature for that type. Bytes outside the signature window are
    /// deliberately set to a safe filler (<c>0x20</c>, a space) so a generated "valid" prefix can
    /// never accidentally match a dangerous executable/HTML signature anchored at offset 0.
    /// </summary>
    private static readonly ValidType[] s_validTypes =
    {
        new("image/jpeg", MediaCategory.Image, () => Prefix(0xFF, 0xD8, 0xFF)),
        new("image/png", MediaCategory.Image, () => Prefix(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)),
        new("image/gif", MediaCategory.Image, () => Prefix(Ascii("GIF89a"))),
        new("image/webp", MediaCategory.Image, () => PrefixAt(8, Ascii("WEBP"))),
        new("video/mp4", MediaCategory.Video, () => PrefixAt(4, Ascii("ftyp"))),
        new("video/quicktime", MediaCategory.Video, () => PrefixAt(4, Ascii("ftyp"))),
    };

    /// <summary>Generates a valid allowlist entry uniformly.</summary>
    private static readonly Gen<ValidType> s_genValidType =
        Gen.Int[0, s_validTypes.Length - 1].Select(index => s_validTypes[index]);

    /// <summary>
    /// Generates a content type that is <em>not</em> on the allowlist. A fixed non-allowlisted
    /// prefix guarantees the string is never one of the six allowed types regardless of the random
    /// suffix.
    /// </summary>
    private static readonly Gen<string> s_genDisallowedContentType =
        Gen.String[Gen.Char.AlphaNumeric, 0, 20]
            .Select(suffix => "application/x-" + suffix);

    [Fact]
    public void Validate_AllowlistedTypeCorrectSignatureWithinBound_AcceptsWithCategory()
    {
        s_genValidType
            .SelectMany(type => SizeWithinBound(type.Category).Select(size => (type, size)))
            .Sample(
                input =>
                {
                    (ValidType type, long size) = input;
                    byte[] leading = type.CorrectPrefix();

                    MediaUploadValidationResult result =
                        MediaUploadValidationPolicy.Validate(type.MimeType, size, leading, s_limits);

                    Assert.True(result.IsAccepted, $"Expected accept for {type.MimeType}, got {result.Reason}.");
                    Assert.Equal(MediaUploadRejectionReason.None, result.Reason);
                    Assert.Equal(type.Category, result.Category);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_ValidSignatureButSizeOverBound_RejectsSizeExceeded()
    {
        s_genValidType
            .SelectMany(type => SizeAboveBound(type.Category).Select(size => (type, size)))
            .Sample(
                input =>
                {
                    (ValidType type, long size) = input;
                    byte[] leading = type.CorrectPrefix();

                    MediaUploadValidationResult result =
                        MediaUploadValidationPolicy.Validate(type.MimeType, size, leading, s_limits);

                    Assert.True(result.IsRejected);
                    Assert.Equal(MediaUploadRejectionReason.SizeExceeded, result.Reason);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_AllowlistedTypeWrongSignature_RejectsSignatureMismatch()
    {
        Gen.Select(s_genValidType, GenNonMatchingPrefix())
            .SelectMany(
                tuple => SizeWithinBound(tuple.Item1.Category).Select(size => (tuple.Item1, tuple.Item2, size)))
            .Sample(
                input =>
                {
                    (ValidType type, byte[] wrong, long size) = input;

                    MediaUploadValidationResult result =
                        MediaUploadValidationPolicy.Validate(type.MimeType, size, wrong, s_limits);

                    Assert.True(result.IsRejected, $"Expected reject for {type.MimeType} with a non-matching prefix.");
                    Assert.Equal(MediaUploadRejectionReason.SignatureMismatch, result.Reason);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_ContentTypeNotOnAllowlist_RejectsDisallowedContentType()
    {
        Gen.Select(s_genDisallowedContentType, GenSafePrefix(), Gen.Long[1L, MediaSizeLimits.DefaultVideoMaxBytes])
            .Sample(
                input =>
                {
                    (string contentType, byte[] leading, long size) = input;

                    MediaUploadValidationResult result =
                        MediaUploadValidationPolicy.Validate(contentType, size, leading, s_limits);

                    Assert.True(result.IsRejected);
                    Assert.Equal(MediaUploadRejectionReason.DisallowedContentType, result.Reason);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_DangerousLeadingBytes_RejectsDangerousContentEvenForAllowlistedType()
    {
        Gen.Select(s_genValidType, GenDangerousPrefix(), Gen.Long[1L, 1024L])
            .Sample(
                input =>
                {
                    (ValidType type, byte[] dangerous, long size) = input;

                    // The claimed content type is allowlisted, but the dangerous-content guard runs
                    // first, so the upload is rejected as dangerous regardless.
                    MediaUploadValidationResult result =
                        MediaUploadValidationPolicy.Validate(type.MimeType, size, dangerous, s_limits);

                    Assert.True(result.IsRejected);
                    Assert.Equal(MediaUploadRejectionReason.DangerousContent, result.Reason);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_EmptyContentOrNonPositiveSize_RejectsEmptyContent()
    {
        // Either empty leading bytes (with any positive size) or a non-positive declared size
        // (with any leading bytes) is treated as empty content.
        Gen.Select(s_genValidType, Gen.Bool, Gen.Long[-1024L, 0L])
            .Sample(
                input =>
                {
                    (ValidType type, bool emptyBytes, long nonPositiveSize) = input;

                    byte[] leading = emptyBytes ? Array.Empty<byte>() : type.CorrectPrefix();
                    long size = emptyBytes ? 1L : nonPositiveSize;

                    MediaUploadValidationResult result =
                        MediaUploadValidationPolicy.Validate(type.MimeType, size, leading, s_limits);

                    Assert.True(result.IsRejected);
                    Assert.Equal(MediaUploadRejectionReason.EmptyContent, result.Reason);
                },
                iter: Iterations);
    }

    private static Gen<long> SizeWithinBound(MediaCategory category)
    {
        long max = s_limits.MaxBytesFor(category);
        return Gen.Long[1L, max];
    }

    private static Gen<long> SizeAboveBound(MediaCategory category)
    {
        long max = s_limits.MaxBytesFor(category);
        return Gen.Long[max + 1L, long.MaxValue];
    }

    /// <summary>
    /// Generates a leading-byte prefix that matches <em>none</em> of the allowlist signatures and
    /// none of the dangerous signatures. It is a fixed-length run of a single safe filler byte
    /// (<c>0x20</c>), which cannot form "ftyp"/"moov"/"WEBP"/the image magics nor any
    /// executable/HTML marker.
    /// </summary>
    private static Gen<byte[]> GenNonMatchingPrefix() =>
        Gen.Int[MediaUploadValidationPolicy.SignatureProbeLength, 64]
            .Select(length => FillerBytes(length));

    /// <summary>
    /// Generates a safe, non-dangerous prefix of probe length filled with a single filler byte;
    /// used where the content type (not the signature) is the subject under test.
    /// </summary>
    private static Gen<byte[]> GenSafePrefix() =>
        Gen.Int[MediaUploadValidationPolicy.SignatureProbeLength, 64]
            .Select(length => FillerBytes(length));

    /// <summary>
    /// Generates a leading-byte prefix that matches one of the executable or HTML/script
    /// signatures the policy rejects outright (<c>MZ</c>, ELF, <c>#!</c>, and the HTML/script
    /// opening markers), with safe filler after the marker.
    /// </summary>
    private static Gen<byte[]> GenDangerousPrefix()
    {
        byte[][] markers =
        {
            Ascii("MZ"),
            new byte[] { 0x7F, 0x45, 0x4C, 0x46 },
            Ascii("#!"),
            Ascii("<!DOCTYPE"),
            Ascii("<html"),
            Ascii("<?xml"),
            Ascii("<script"),
            Ascii("<svg"),
            Ascii("<HTML"),
            Ascii("<SCRIPT"),
            Ascii("<SVG"),
        };

        return Gen.Int[0, markers.Length - 1].Select(index => Prefix(markers[index]));
    }

    /// <summary>Builds a probe-length prefix with <paramref name="pattern"/> at offset 0.</summary>
    private static byte[] Prefix(params byte[] pattern) => PrefixAt(0, pattern);

    /// <summary>
    /// Builds a probe-length prefix (at least <see cref="MediaUploadValidationPolicy.SignatureProbeLength"/>
    /// bytes) with <paramref name="pattern"/> copied in at <paramref name="offset"/> and filler
    /// elsewhere.
    /// </summary>
    private static byte[] PrefixAt(int offset, params byte[] pattern)
    {
        int length = Math.Max(MediaUploadValidationPolicy.SignatureProbeLength, offset + pattern.Length);
        byte[] buffer = FillerBytes(length);
        Array.Copy(pattern, 0, buffer, offset, pattern.Length);
        return buffer;
    }

    private static byte[] FillerBytes(int length)
    {
        byte[] buffer = new byte[length];
        Array.Fill(buffer, (byte)0x20);
        return buffer;
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary>One allowlisted content type with its category and a correct-signature factory.</summary>
    private sealed record ValidType(string MimeType, MediaCategory Category, Func<byte[]> CorrectPrefix);
}
