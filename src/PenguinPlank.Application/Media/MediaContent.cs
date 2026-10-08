namespace PenguinPlank.Application.Media;

/// <summary>
/// A readable handle to a stored media file's binary content, returned by
/// <see cref="IFileStore.OpenAsync"/> for an <b>already-authorized</b> download (requirement 6.11 /
/// A4).
/// </summary>
/// <remarks>
/// <para>
/// The file store opens the content only after the caller has authorized the request at the API
/// boundary; the store itself never consults media visibility and never bypasses authorization.
/// Public-approved metadata is a publishing state, not an anonymous-access grant — the API still
/// requires an authenticated, authorized actor before calling the store (requirement 6.11).
/// </para>
/// <para>
/// <see cref="MediaContent"/> owns its underlying <see cref="Stream"/> and disposes it when
/// disposed. The caller is responsible for disposing this handle (for example after the response
/// body has been written).
/// </para>
/// </remarks>
public sealed class MediaContent : System.IDisposable
{
    /// <summary>
    /// Creates a media content handle.
    /// </summary>
    /// <param name="stream">The readable content stream; ownership transfers to this handle.</param>
    /// <param name="contentType">The stored content type for the response.</param>
    /// <param name="sizeBytes">The content length in bytes.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="contentType"/> is blank.</exception>
    public MediaContent(System.IO.Stream stream, string contentType, long sizeBytes)
    {
        System.ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new System.ArgumentException("Media content requires a content type.", nameof(contentType));
        }

        Stream = stream;
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
    }

    /// <summary>The readable content stream, owned by this handle.</summary>
    public System.IO.Stream Stream { get; }

    /// <summary>The stored content type for the response.</summary>
    public string ContentType { get; }

    /// <summary>The content length in bytes.</summary>
    public long SizeBytes { get; }

    /// <summary>Disposes the underlying content stream.</summary>
    public void Dispose() => Stream.Dispose();
}
