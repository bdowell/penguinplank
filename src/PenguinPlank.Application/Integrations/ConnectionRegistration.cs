using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The details recorded for a new platform connection (requirement R11 / 3.1). A new connection
/// always starts disabled with unknown health and no credential; those are not inputs.
/// </summary>
/// <param name="Platform">The external platform, for example "Shopify".</param>
/// <param name="Account">The business account or shop identity on the platform.</param>
/// <param name="Scopes">The granted scopes in the platform's scope string.</param>
/// <param name="ApiVersion">The platform API version targeted.</param>
/// <param name="EnabledCapabilities">The enabled capabilities, for example "orders,inventory".</param>
/// <param name="SyncDirection">The permitted synchronization direction.</param>
public sealed record ConnectionRegistration(
    string Platform,
    string Account,
    string Scopes,
    string ApiVersion,
    string EnabledCapabilities,
    SyncDirection SyncDirection);
