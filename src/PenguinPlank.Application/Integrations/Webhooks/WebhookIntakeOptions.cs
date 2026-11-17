namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>Limits applied to inbound provider callbacks (requirement 9.1).</summary>
/// <param name="MaxPayloadBytes">The inclusive maximum body size in bytes; positive.</param>
public sealed record WebhookIntakeOptions(int MaxPayloadBytes)
{
    /// <summary>The default limit: 1 MiB.</summary>
    public const int DefaultMaxPayloadBytes = 1024 * 1024;

    /// <summary>The default options.</summary>
    public static WebhookIntakeOptions Default { get; } = new(DefaultMaxPayloadBytes);
}
