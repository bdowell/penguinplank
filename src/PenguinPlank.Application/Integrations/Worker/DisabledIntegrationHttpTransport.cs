namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>
/// The Phase A transport. No connector is enabled (requirement 3.17), so any attempt to reach a
/// provider is a programming error and fails loudly instead of making a network call.
/// </summary>
public sealed class DisabledIntegrationHttpTransport : IIntegrationHttpTransport
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Always thrown; provider connectors are disabled in Phase A.</exception>
    public Task<IntegrationHttpResponse> SendAsync(IntegrationHttpRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Provider connectors are disabled in Phase A; no HTTP request is sent.");
    }
}
