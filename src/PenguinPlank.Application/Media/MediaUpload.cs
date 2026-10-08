namespace PenguinPlank.Application.Media;

/// <summary>
/// A media upload presented to the <see cref="IFileStore"/>: the claimed content type, the declared
/// size, and the readable content stream (requirements 6.8, 6.9, 6.10 / A4).
/// </summary>
/// <remarks>
/// <para>
/// The upload carries the <em>claimed</em> content type and declared size supplied by the caller;
/// the store validates both against the content's actual leading bytes using the pure
/// <c>MediaUploadValidationPolicy</c> before persisting anything. The <see cref="OriginalFileName"/>
/// is kept only for an optional caption/audit; it is <b>never</b> used to form the storage key
/// (requirement 6.9).
/// </para>
/// <para>
/// <see cref="Content"/> is owned by the caller; the store reads from it but does not dispose it.
/// This is a transport value object with no business decision of its own.
/// </para>
/// </remarks>
public sealed class MediaUpload
{
    /// <summary>
    /// Creates an upload.
    /// </summary>
    /// <param name="claimedContentType">The content type the caller claims for the file.</param>
    /// <param name="declaredSizeBytes">The declared size of the complete file, in bytes.</param>
    /// <param name="content">A readable stream positioned at the start of the content.</param>
    /// <param name="originalFileName">The optional original filename, for caption/audit only.</param>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="claimedContentType"/> is blank.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    public MediaUpload(string claimedContentType, long declaredSizeBytes, Stream content, string? originalFileName = null)
    {
        if (string.IsNullOrWhiteSpace(claimedContentType))
        {
            throw new System.ArgumentException("An upload requires a claimed content type.", nameof(claimedContentType));
        }

        System.ArgumentNullException.ThrowIfNull(content);

        ClaimedContentType = claimedContentType.Trim();
        DeclaredSizeBytes = declaredSizeBytes;
        Content = content;
        OriginalFileName = originalFileName;
    }

    /// <summary>The content type the caller claims for the file.</summary>
    public string ClaimedContentType { get; }

    /// <summary>The declared size of the complete file, in bytes.</summary>
    public long DeclaredSizeBytes { get; }

    /// <summary>The readable content stream, owned by the caller.</summary>
    public Stream Content { get; }

    /// <summary>The optional original filename, retained for caption/audit only — never for the storage key.</summary>
    public string? OriginalFileName { get; }
}
