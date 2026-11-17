namespace PenguinPlank.Contracts.Integrations;

/// <summary>
/// The versioned request body an Owner submits to record a platform connection (requirement R11 / 3.1).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO. It carries no credential: a connection is always recorded disabled,
/// with unknown health, and any credential is attached through the credential-reference indirection,
/// never through this surface.
/// </remarks>
public sealed record RegisterConnectionContract
{
    /// <summary>The external platform, for example "Shopify".</summary>
    public required string Platform { get; init; }

    /// <summary>The business account or shop identity on the platform.</summary>
    public required string Account { get; init; }

    /// <summary>The granted scopes in the platform's scope string.</summary>
    public required string Scopes { get; init; }

    /// <summary>The platform API version targeted.</summary>
    public required string ApiVersion { get; init; }

    /// <summary>The enabled capabilities, for example "orders,inventory".</summary>
    public required string EnabledCapabilities { get; init; }

    /// <summary>The permitted synchronization direction as a stable string: None, Inbound, Outbound or Bidirectional.</summary>
    public required string SyncDirection { get; init; }
}
