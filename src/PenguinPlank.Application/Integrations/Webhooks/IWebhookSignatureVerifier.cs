namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// The narrow boundary that authenticates a provider callback (requirements 3.7, 9.2).
/// </summary>
/// <remarks>
/// Provider-specific signature schemes live behind this interface; none are implemented in Phase
/// A. An implementation must be fast, side-effect free, and must reject (not throw) on a missing
/// or malformed signature.
/// </remarks>
public interface IWebhookSignatureVerifier
{
    /// <summary>Verifies the callback's signature or authentication.</summary>
    /// <param name="callback">The callback, including raw body and headers.</param>
    /// <returns>An authentic outcome with the delivery key, or a rejected outcome.</returns>
    WebhookVerification Verify(WebhookCallback callback);
}
