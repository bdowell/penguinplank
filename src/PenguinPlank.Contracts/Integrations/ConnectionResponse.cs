namespace PenguinPlank.Contracts.Integrations;

/// <summary>
/// The versioned transport shape returned for a platform connection on the Owner-only
/// <c>/integrations/connections</c> surface (requirement R11 / 3.2, 3.12).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO. It deliberately carries <b>no credential reference and no secret</b>:
/// only whether a credential exists and its lifecycle state are exposed.
/// </remarks>
public sealed record ConnectionResponse
{
    /// <summary>The connection identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The external platform.</summary>
    public required string Platform { get; init; }

    /// <summary>The account or shop identity.</summary>
    public required string Account { get; init; }

    /// <summary>The granted scopes.</summary>
    public required string Scopes { get; init; }

    /// <summary>The platform API version.</summary>
    public required string ApiVersion { get; init; }

    /// <summary>The enabled capabilities.</summary>
    public required string EnabledCapabilities { get; init; }

    /// <summary>The permitted synchronization direction as a stable string.</summary>
    public required string SyncDirection { get; init; }

    /// <summary>The observed health as a stable string.</summary>
    public required string Health { get; init; }

    /// <summary>Whether the connection is enabled. Always false in Phase A.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Whether a credential reference is associated.</summary>
    public required bool HasCredential { get; init; }

    /// <summary>The recorded credential state as a stable string.</summary>
    public required string CredentialState { get; init; }

    /// <summary>When the credential state last changed, if ever.</summary>
    public DateTimeOffset? CredentialStateChangedAtUtc { get; init; }
}
