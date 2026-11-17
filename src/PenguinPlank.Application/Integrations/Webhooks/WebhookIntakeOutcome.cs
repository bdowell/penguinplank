namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>The outcome of handling an inbound provider callback.</summary>
public enum WebhookIntakeOutcome
{
    /// <summary>The callback was authentic and newly recorded durably.</summary>
    Recorded,

    /// <summary>The callback was authentic and its delivery key was already recorded.</summary>
    Duplicate,

    /// <summary>The body exceeded the payload limit. Nothing was recorded.</summary>
    PayloadTooLarge,

    /// <summary>The provider name is malformed. Nothing was recorded.</summary>
    UnknownProvider,

    /// <summary>The signature was missing, invalid, or forged. Nothing was recorded.</summary>
    Rejected,

    /// <summary>The callback was authentic but no connection exists for the provider. Nothing was recorded.</summary>
    UnknownConnection,
}
