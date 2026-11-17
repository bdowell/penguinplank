using Microsoft.Extensions.Options;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Application.Integrations.Webhooks;
using PenguinPlank.Application.Media;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// Stores webhook payloads as files under the configured file-store root (outside the web root).
/// </summary>
/// <remarks>
/// The reference is deterministic (<c>webhooks/{provider}/{hash}</c>), so an existing file means the
/// delivery was already stored and it is never overwritten. The path is rebuilt from validated
/// segments only; a reference that does not match the expected shape is refused.
/// </remarks>
public sealed class LocalWebhookPayloadStore : IWebhookPayloadStore
{
    private const string Prefix = "webhooks/";
    private const int HashLength = 64;

    private readonly string _rootPath;

    /// <summary>Creates the store.</summary>
    /// <param name="options">The file-store options supplying the root path.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when no root path is configured.</exception>
    public LocalWebhookPayloadStore(IOptions<FileStoreOptions> options)
    {
        System.ArgumentNullException.ThrowIfNull(options);

        string? root = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new System.InvalidOperationException("FileStore:RootPath is required.");
        }

        _rootPath = Path.GetFullPath(root);
    }

    /// <inheritdoc />
    public async Task SaveIfAbsentAsync(
        PayloadRef reference,
        System.ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        string path = ResolvePath(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);
            await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Already stored by an earlier delivery of the same key; keep the original.
        }
    }

    private string ResolvePath(PayloadRef reference)
    {
        string value = reference.Value;
        string[] parts = value.Split('/');

        bool valid = value.StartsWith(Prefix, System.StringComparison.Ordinal)
            && parts.Length == 3
            && WebhookPayloadReference.IsValidProvider(parts[1])
            && parts[2].Length == HashLength
            && parts[2].All(char.IsAsciiHexDigitLower);

        if (!valid)
        {
            throw new System.ArgumentException("The payload reference is not a webhook reference.", nameof(reference));
        }

        return Path.Combine(_rootPath, "webhooks", parts[1], parts[2] + ".bin");
    }
}
