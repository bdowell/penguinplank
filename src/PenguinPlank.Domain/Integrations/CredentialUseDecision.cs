namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// Whether a connection's credential may be used, and if not, why (requirement R11 / 3.3).
/// </summary>
public enum CredentialUseDecision
{
    /// <summary>A credential reference exists and its state is active.</summary>
    Usable = 0,

    /// <summary>No credential reference has been associated with the connection.</summary>
    NoCredential = 1,

    /// <summary>The credential was revoked; it must not be used.</summary>
    Revoked = 2,

    /// <summary>The credential expired; it must not be used.</summary>
    Expired = 3,
}
