using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// A read view of a recorded connection. It deliberately omits the credential reference: only
/// whether one exists and its state are exposed, so the locator cannot reach a response or log
/// through this type (requirement R11 / 3.2).
/// </summary>
/// <param name="Id">The connection identifier.</param>
/// <param name="Platform">The external platform.</param>
/// <param name="Account">The account or shop identity.</param>
/// <param name="Scopes">The granted scopes.</param>
/// <param name="ApiVersion">The platform API version.</param>
/// <param name="EnabledCapabilities">The enabled capabilities.</param>
/// <param name="SyncDirection">The permitted synchronization direction.</param>
/// <param name="Health">The observed health.</param>
/// <param name="Enabled">Whether the connection is enabled. Always false in Phase A.</param>
/// <param name="HasCredential">Whether a credential reference is associated.</param>
/// <param name="CredentialState">The recorded credential state.</param>
/// <param name="CredentialStateChangedAtUtc">When the credential state last changed, if ever.</param>
public sealed record IntegrationConnectionSummary(
    Guid Id,
    string Platform,
    string Account,
    string Scopes,
    string ApiVersion,
    string EnabledCapabilities,
    SyncDirection SyncDirection,
    ConnectionHealth Health,
    bool Enabled,
    bool HasCredential,
    CredentialState CredentialState,
    DateTimeOffset? CredentialStateChangedAtUtc);
