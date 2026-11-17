namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// The Phase A default verifier. Connectors are disabled, so every callback is rejected.
/// </summary>
public sealed class DisabledWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    /// <inheritdoc />
    public WebhookVerification Verify(WebhookCallback callback)
    {
        System.ArgumentNullException.ThrowIfNull(callback);

        return WebhookVerification.Rejected();
    }
}
