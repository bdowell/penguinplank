using PenguinPlank.Domain.Media;

namespace PenguinPlank.Application.Media;

/// <summary>
/// Validated typed options for the local <c>IFileStore</c> adapter: the root directory the media
/// binaries live under and the enforced size bounds (requirements 6.8, 6.10 / A4).
/// </summary>
/// <remarks>
/// <para>
/// These values are bound from configuration and validated at the composition root (coding-standards
/// §2); business and infrastructure code receive this object rather than reading a configuration
/// global. The <see cref="RootPath"/> <b>must point outside the web root</b> so a media file can
/// never be served as a static asset or discovered by URL guessing — the API serves media only
/// through the authorized download endpoint (requirements 6.8, 6.11). Deployment documentation and
/// the adapter both state this requirement; <see cref="Validate"/> rejects a missing root path at
/// startup.
/// </para>
/// <para>
/// <see cref="ImageMaxBytes"/> and <see cref="VideoMaxBytes"/> default to the business settings of
/// 20 MB and 200 MB (requirement 6.10) and feed the pure <see cref="MediaUploadValidationPolicy"/>
/// through <see cref="SizeLimits"/>.
/// </para>
/// </remarks>
public sealed class FileStoreOptions
{
    /// <summary>The configuration section these options bind from (for example <c>FileStore:RootPath</c>).</summary>
    public const string SectionName = "FileStore";

    /// <summary>
    /// The absolute root directory under which media binaries are stored. Required, and it must be
    /// <b>outside the web root</b> (requirement 6.8). There is no default: a deployment supplies a
    /// path on a protected volume, and startup validation refuses a blank value rather than falling
    /// back to a location inside the application's served content.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>The inclusive maximum size for an image upload, in bytes. Defaults to 20 MB (requirement 6.10).</summary>
    public long ImageMaxBytes { get; set; } = MediaSizeLimits.DefaultImageMaxBytes;

    /// <summary>The inclusive maximum size for a video upload, in bytes. Defaults to 200 MB (requirement 6.10).</summary>
    public long VideoMaxBytes { get; set; } = MediaSizeLimits.DefaultVideoMaxBytes;

    /// <summary>The size bounds as the domain value object the validation policy consumes.</summary>
    /// <returns>A <see cref="MediaSizeLimits"/> built from <see cref="ImageMaxBytes"/> and <see cref="VideoMaxBytes"/>.</returns>
    public MediaSizeLimits SizeLimits() => new(ImageMaxBytes, VideoMaxBytes);

    /// <summary>
    /// Validates the options. Called by the options framework at startup so a misconfiguration fails
    /// fast rather than at the first upload.
    /// </summary>
    /// <returns><see langword="true"/> when the options are usable.</returns>
    /// <param name="failureMessage">On failure, a human-readable reason; otherwise <see langword="null"/>.</param>
    public bool Validate(out string? failureMessage)
    {
        if (string.IsNullOrWhiteSpace(RootPath))
        {
            failureMessage = "FileStore:RootPath is required and must point to a directory outside the web root.";
            return false;
        }

        if (ImageMaxBytes <= 0 || VideoMaxBytes <= 0)
        {
            failureMessage = "FileStore image and video size limits must be positive.";
            return false;
        }

        failureMessage = null;
        return true;
    }
}
