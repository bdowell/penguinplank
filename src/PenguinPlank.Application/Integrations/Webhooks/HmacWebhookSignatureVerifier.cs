namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// A generic header-driven HMAC-SHA256 verifier for one provider. It is not registered in Phase A
/// (connectors are disabled); it exists so provider adapters and tests can reuse one verified
/// scheme.
/// </summary>
public sealed class HmacWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly string _provider;
    private readonly byte[] _secret;
    private readonly string _signatureHeaderName;
    private readonly string _deliveryKeyHeaderName;

    /// <summary>Creates a verifier for one provider.</summary>
    /// <param name="provider">The provider route segment this verifier accepts.</param>
    /// <param name="secret">The shared secret bytes; non-empty.</param>
    /// <param name="signatureHeaderName">The header carrying the signature.</param>
    /// <param name="deliveryKeyHeaderName">The header carrying the unique delivery id.</param>
    /// <exception cref="System.ArgumentException">Thrown when an argument is empty.</exception>
    public HmacWebhookSignatureVerifier(
        string provider,
        byte[] secret,
        string signatureHeaderName,
        string deliveryKeyHeaderName)
    {
        System.ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        System.ArgumentNullException.ThrowIfNull(secret);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(signatureHeaderName);
        System.ArgumentException.ThrowIfNullOrWhiteSpace(deliveryKeyHeaderName);

        if (secret.Length == 0)
        {
            throw new System.ArgumentException("A webhook secret must not be empty.", nameof(secret));
        }

        _provider = provider;
        _secret = (byte[])secret.Clone();
        _signatureHeaderName = signatureHeaderName;
        _deliveryKeyHeaderName = deliveryKeyHeaderName;
    }

    /// <inheritdoc />
    public WebhookVerification Verify(WebhookCallback callback)
    {
        System.ArgumentNullException.ThrowIfNull(callback);

        if (!string.Equals(callback.Provider, _provider, System.StringComparison.OrdinalIgnoreCase))
        {
            return WebhookVerification.Rejected();
        }

        callback.Headers.TryGetValue(_signatureHeaderName, out string? signature);
        if (!HmacSha256Signature.IsValid(_secret, callback.Body.Span, signature))
        {
            return WebhookVerification.Rejected();
        }

        if (!callback.Headers.TryGetValue(_deliveryKeyHeaderName, out string? key)
            || string.IsNullOrWhiteSpace(key)
            || key.Length > DeliveryKey.MaxLength)
        {
            return WebhookVerification.Rejected();
        }

        return WebhookVerification.Authentic(new DeliveryKey(key));
    }
}
