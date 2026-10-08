namespace PenguinPlank.Domain.Media;

/// <summary>
/// The size bounds, in bytes, that a media upload must stay within: a maximum for images and a
/// maximum for videos (requirement 6.10 / A4).
/// </summary>
/// <remarks>
/// <para>
/// This is an immutable value object passed <em>into</em> the pure
/// <see cref="MediaUploadValidationPolicy"/>; the policy never reads a configuration global or a
/// clock. The limits originate from validated typed options bound at the composition root, which
/// default to the business settings of 20 MB for images and 200 MB for videos
/// (<c>BusinessSettings.ImageSizeLimitBytes</c> / <c>VideoSizeLimitBytes</c>). Centralizing the
/// bound here keeps the thresholds out of scattered magic values (coding-standards §5).
/// </para>
/// <para>
/// Both bounds must be positive; a non-positive bound is a configuration/programming mistake, not
/// an expected upload failure, so the constructor throws.
/// </para>
/// </remarks>
public readonly record struct MediaSizeLimits
{
    /// <summary>The Phase A default image size bound: 20 MB.</summary>
    public const long DefaultImageMaxBytes = 20L * 1024 * 1024;

    /// <summary>The Phase A default video size bound: 200 MB.</summary>
    public const long DefaultVideoMaxBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Creates the size bounds.
    /// </summary>
    /// <param name="imageMaxBytes">The inclusive maximum size for an image upload, in bytes. Must be positive.</param>
    /// <param name="videoMaxBytes">The inclusive maximum size for a video upload, in bytes. Must be positive.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when either bound is not positive.
    /// </exception>
    public MediaSizeLimits(long imageMaxBytes, long videoMaxBytes)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageMaxBytes);
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(videoMaxBytes);

        ImageMaxBytes = imageMaxBytes;
        VideoMaxBytes = videoMaxBytes;
    }

    /// <summary>The inclusive maximum size for an image upload, in bytes.</summary>
    public long ImageMaxBytes { get; }

    /// <summary>The inclusive maximum size for a video upload, in bytes.</summary>
    public long VideoMaxBytes { get; }

    /// <summary>The Phase A default bounds (20 MB image, 200 MB video).</summary>
    public static MediaSizeLimits Default => new(DefaultImageMaxBytes, DefaultVideoMaxBytes);

    /// <summary>
    /// The inclusive maximum size, in bytes, for the given <paramref name="category"/>.
    /// </summary>
    /// <param name="category">The media category whose bound is requested.</param>
    /// <returns>The image bound for <see cref="MediaCategory.Image"/>; otherwise the video bound.</returns>
    public long MaxBytesFor(MediaCategory category) =>
        category == MediaCategory.Image ? ImageMaxBytes : VideoMaxBytes;
}
