namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// The outcome of verifying a provider callback's signature or authentication.
/// </summary>
public sealed record WebhookVerification
{
    /// <summary>The signature state recorded on an accepted delivery.</summary>
    public const string VerifiedSignatureState = "Verified";

    private WebhookVerification(bool isAuthentic, DeliveryKey? deliveryKey)
    {
        IsAuthentic = isAuthentic;
        DeliveryKey = deliveryKey;
    }

    /// <summary>Whether the callback is authentic.</summary>
    public bool IsAuthentic { get; }

    /// <summary>The provider delivery key; present exactly when the callback is authentic.</summary>
    public DeliveryKey? DeliveryKey { get; }

    /// <summary>Creates an authentic outcome carrying the provider's delivery key.</summary>
    /// <param name="deliveryKey">The provider delivery key used for deduplication.</param>
    /// <returns>An authentic verification.</returns>
    public static WebhookVerification Authentic(DeliveryKey deliveryKey) => new(true, deliveryKey);

    /// <summary>Creates a rejected outcome for a missing, invalid, or forged signature.</summary>
    /// <returns>A rejected verification.</returns>
    public static WebhookVerification Rejected() => new(false, null);
}
