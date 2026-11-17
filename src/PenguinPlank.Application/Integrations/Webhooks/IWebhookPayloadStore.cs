namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// Durably holds the raw body of an authenticated callback outside the inbox row.
/// </summary>
public interface IWebhookPayloadStore
{
    /// <summary>Stores a payload under a reference unless one already exists there.</summary>
    /// <param name="reference">The deterministic payload reference.</param>
    /// <param name="body">The raw body.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the payload is durable (or already present).</returns>
    Task SaveIfAbsentAsync(
        PayloadRef reference,
        System.ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken);
}
