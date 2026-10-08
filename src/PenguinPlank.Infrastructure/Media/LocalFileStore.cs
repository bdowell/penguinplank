using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using PenguinPlank.Application.Media;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// The Phase A local-filesystem <see cref="IFileStore"/>: stores media binaries under a configured
/// root directory <b>outside the web root</b>, validates every upload with the pure
/// <see cref="MediaUploadValidationPolicy"/>, assigns randomized storage keys, and serves content
/// only to callers that have already been authorized at the API boundary (requirements 6.8â€“6.11 /
/// A4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Outside the web root.</b> The root directory comes from validated <see cref="FileStoreOptions"/>
/// (which require a non-blank path and document that it must sit outside the served content). The
/// adapter stores files there and resolves every key back under that root, rejecting any key that
/// would escape it â€” so no media file is reachable as a static asset or by URL guessing
/// (requirements 6.8, 6.11).
/// </para>
/// <para>
/// <b>Randomized keys.</b> The injected <see cref="IStorageKeyGenerator"/> produces a GUID-based key
/// that is never derived from the client filename (requirement 6.9).
/// </para>
/// <para>
/// <b>Validate before write.</b> <see cref="SaveAsync"/> buffers the leading bytes, runs the pure
/// validation policy (allowlist + magic-byte signature + reject executable/HTML + size bound), and
/// returns an <see cref="ErrorCode.UploadRejected"/> failure without writing anything when the
/// upload is rejected. On acceptance it streams the content to the keyed file and records the actual
/// byte count and a SHA-256 checksum.
/// </para>
/// <para>
/// <b>Authorized access only.</b> <see cref="OpenAsync"/> opens a stream for a request the API has
/// already authorized; the store never consults <c>MediaVisibility</c> and never grants anonymous
/// access. Public-approved is a publishing/metadata state, not an access grant (requirement 6.11).
/// </para>
/// <para>
/// The adapter is stateless apart from its injected options and key generator, so the composition
/// root registers it as a singleton (it captures no scoped <c>DbContext</c>; coding-standards Â§2,
/// requirement A2 Â§5.14).
/// </para>
/// </remarks>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _rootPath;
    private readonly MediaSizeLimits _sizeLimits;
    private readonly IStorageKeyGenerator _keyGenerator;

    /// <summary>
    /// Creates the local file store.
    /// </summary>
    /// <param name="options">The validated file-store options (root path + size bounds).</param>
    /// <param name="keyGenerator">The randomized storage-key generator.</param>
    /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured root path is blank. Dependency validation only â€” the constructor
    /// performs no I/O (coding-standards Â§2).
    /// </exception>
    public LocalFileStore(IOptions<FileStoreOptions> options, IStorageKeyGenerator keyGenerator)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(keyGenerator);

        FileStoreOptions value = options.Value;
        if (string.IsNullOrWhiteSpace(value.RootPath))
        {
            throw new InvalidOperationException(
                "FileStore:RootPath must be configured with a directory outside the web root.");
        }

        _rootPath = Path.GetFullPath(value.RootPath);
        _sizeLimits = value.SizeLimits();
        _keyGenerator = keyGenerator;
    }

    /// <inheritdoc />
    public async Task<Result<StoredMedia>> SaveAsync(MediaUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);

        // Buffer the leading bytes for the signature check without consuming the whole stream.
        byte[] probe = new byte[MediaUploadValidationPolicy.SignatureProbeLength];
        int probeLength = await ReadLeadingBytesAsync(upload.Content, probe, cancellationToken).ConfigureAwait(false);

        MediaUploadValidationResult validation = MediaUploadValidationPolicy.Validate(
            upload.ClaimedContentType,
            upload.DeclaredSizeBytes,
            probe.AsSpan(0, probeLength),
            _sizeLimits);

        if (validation.IsRejected)
        {
            // Expected business failure: nothing is written.
            return Result.Failure<StoredMedia>(ErrorCode.UploadRejected, validation.Message);
        }

        StorageKey key = _keyGenerator.NewKey();
        Directory.CreateDirectory(_rootPath);
        string path = ResolvePath(key);

        long bytesWritten;
        string checksum;
        try
        {
            (bytesWritten, checksum) = await WriteAndHashAsync(
                upload,
                probe,
                probeLength,
                path,
                validation.Category,
                cancellationToken).ConfigureAwait(false);
        }
        catch (MediaSizeExceededException)
        {
            // The content ran past its category bound as it streamed in. Remove the partial file so
            // a rejected upload leaves no trace, then report the expected business failure.
            TryDelete(path);
            return Result.Failure<StoredMedia>(
                ErrorCode.UploadRejected,
                "The upload exceeds the maximum allowed size for its media type.");
        }
        catch
        {
            TryDelete(path);
            throw;
        }

        var stored = new StoredMedia(key, upload.ClaimedContentType, bytesWritten, validation.Category, checksum);
        return Result.Success(stored);
    }

    /// <inheritdoc />
    public Task<Result<MediaContent>> OpenAsync(StorageKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult(Result.Failure<MediaContent>(
                ErrorCode.Validation,
                "The requested media file does not exist."));
        }

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        // Content type is tracked in metadata (MediaAsset); the handle reports octet-stream here,
        // and the API supplies the authoritative content type from the metadata record when it
        // writes the authorized response (requirement 6.11).
        var media = new MediaContent(stream, "application/octet-stream", stream.Length);
        return Task.FromResult(Result.Success(media));
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(StorageKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TryDelete(ResolvePath(key));

        // Deleting an already-absent file is a success (idempotent).
        return Task.FromResult(Result.Success());
    }

    private static async Task<int> ReadLeadingBytesAsync(Stream content, byte[] buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await content.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private async Task<(long BytesWritten, string Checksum)> WriteAndHashAsync(
        MediaUpload upload,
        byte[] probe,
        int probeLength,
        string path,
        MediaCategory category,
        CancellationToken cancellationToken)
    {
        long maxBytes = _sizeLimits.MaxBytesFor(category);
        long total = 0;

        using var hasher = SHA256.Create();
        await using var destination = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        // First flush the already-buffered leading bytes, then stream the remainder.
        if (probeLength > 0)
        {
            total = await AppendAsync(destination, hasher, probe, 0, probeLength, total, maxBytes, cancellationToken)
                .ConfigureAwait(false);
        }

        byte[] buffer = new byte[81920];
        int read;
        while ((read = await upload.Content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total = await AppendAsync(destination, hasher, buffer, 0, read, total, maxBytes, cancellationToken)
                .ConfigureAwait(false);
        }

        hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        string checksum = Convert.ToHexStringLower(hasher.Hash!);
        return (total, checksum);
    }

    private static async Task<long> AppendAsync(
        Stream destination,
        SHA256 hasher,
        byte[] buffer,
        int offset,
        int count,
        long runningTotal,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        long updated = runningTotal + count;
        if (updated > maxBytes)
        {
            // Defend against a declared size that understated the real content length.
            throw new MediaSizeExceededException();
        }

        hasher.TransformBlock(buffer, offset, count, outputBuffer: null, outputOffset: 0);
        await destination.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private string ResolvePath(StorageKey key)
    {
        // The key is an opaque GUID-N token; combine under the root and confirm the resolved path
        // stays within the root so a crafted key can never escape the media directory.
        string candidate = Path.GetFullPath(Path.Combine(_rootPath, key.Value));
        string rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The resolved media path escapes the configured root.");
        }

        return candidate;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a transient delete failure must not mask the original outcome.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Signals, during streaming, that the content ran past its category size bound. Translated by
    /// <see cref="SaveAsync"/> into an <see cref="ErrorCode.UploadRejected"/> business failure.
    /// </summary>
    private sealed class MediaSizeExceededException : Exception
    {
    }
}
