using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A record of a connected (or connectable) external platform account: the platform, account or
/// shop identity, granted scopes, API version, enabled capabilities, sync direction, health, and
/// a reference (never the value) to the external credential.
/// </summary>
/// <remarks>
/// <para>
/// A connection stores the business account or shop identity, granted scopes, API version,
/// enabled capabilities, sync direction, and connection health (requirement 3.1 / R11).
/// </para>
/// <para>
/// <b>Connectors are disabled in Phase A.</b> <see cref="Enabled"/> therefore defaults to
/// <see langword="false"/> — enforced both as a POCO default here and as an EF
/// <c>HasDefaultValue(false)</c> in the configuration — so a connection recorded outside a use
/// case cannot start life performing synchronization (requirement 3.17; design "connectors
/// disabled").
/// </para>
/// <para>
/// <b>The credential secret is never a column.</b> Only a <see cref="CredentialReference"/> — a
/// pointer to a secret held encrypted or in the deployment secret store — is persisted. The secret
/// value itself is never stored in the database, source control, browser storage, logs, or API
/// responses (requirement 3.2 / A4; the secret lives outside the database). Revocation/expiration
/// state is recorded by a later task so a revoked credential stops being used (requirement 3.3).
/// </para>
/// <para>
/// It derives from <see cref="VersionedEntity"/>: a connection is a mutable aggregate (health,
/// scopes, enabled capabilities, and the enabled flag change over time) and must be protected
/// from stale overwrites.
/// </para>
/// </remarks>
public class IntegrationConnection : VersionedEntity
{
    /// <summary>The external platform (for example, "Shopify", "Square", "Etsy").</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>The business account or shop identity on the platform (requirement 3.1).</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>The granted OAuth scopes, stored as the platform-prescribed scope string.</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>The platform API version this connection targets.</summary>
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>The capabilities enabled for this connection (for example, "orders,inventory").</summary>
    public string EnabledCapabilities { get; set; } = string.Empty;

    /// <summary>The direction synchronization is permitted to flow. Defaults to <see cref="SyncDirection.None"/>.</summary>
    public SyncDirection SyncDirection { get; set; } = SyncDirection.None;

    /// <summary>The observed health of the connection. Defaults to <see cref="ConnectionHealth.Unknown"/>.</summary>
    public ConnectionHealth Health { get; set; } = ConnectionHealth.Unknown;

    /// <summary>
    /// Whether the connection is enabled for synchronization. <b>Defaults to
    /// <see langword="false"/></b> in Phase A — connectors are disabled (requirement 3.17).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// A reference to the external credential secret (held encrypted or in the deployment secret
    /// store). <b>Never the secret value itself</b> (requirement 3.2). Null until a credential is
    /// associated.
    /// </summary>
    public string? CredentialReference { get; set; }
}
