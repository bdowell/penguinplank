namespace PenguinPlank.Domain.Media;

/// <summary>
/// The broad category a media upload belongs to, which selects the applicable size bound
/// (requirement 6.10 / A4).
/// </summary>
/// <remarks>
/// The category is derived from the allowlisted content type during validation rather than
/// supplied by the caller: an image content type yields <see cref="Image"/> (20 MB bound) and a
/// video content type yields <see cref="Video"/> (200 MB bound). A content type outside the
/// allowlist never produces a category — it is rejected before a size bound is chosen.
/// </remarks>
public enum MediaCategory
{
    /// <summary>A still image (JPEG, PNG, WebP, or GIF). Bounded at the image size limit.</summary>
    Image = 0,

    /// <summary>A video (MP4 or QuickTime). Bounded at the video size limit.</summary>
    Video = 1,
}
