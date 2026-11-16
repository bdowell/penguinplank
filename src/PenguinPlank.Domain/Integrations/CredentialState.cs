namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// The recorded lifecycle state of the external credential an <see cref="IntegrationConnection"/>
/// refers to (requirement R11 / 3.3).
/// </summary>
public enum CredentialState
{
    /// <summary>The credential reference has not been revoked or expired. The default.</summary>
    Active = 0,

    /// <summary>The credential was revoked and must not be used.</summary>
    Revoked = 1,

    /// <summary>The credential expired and must not be used.</summary>
    Expired = 2,
}
