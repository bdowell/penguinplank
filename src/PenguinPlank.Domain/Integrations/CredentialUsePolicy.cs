namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// The pure rule deciding whether a connection's credential may be used. A revoked or expired
/// credential is never usable, so the system stops using it (requirement R11 / 3.3).
/// </summary>
public static class CredentialUsePolicy
{
    /// <summary>Decides whether the credential may be used.</summary>
    /// <param name="credentialReference">The stored credential reference, or null when none is associated.</param>
    /// <param name="state">The recorded credential state.</param>
    /// <returns>The decision; only <see cref="CredentialUseDecision.Usable"/> permits use.</returns>
    public static CredentialUseDecision Decide(string? credentialReference, CredentialState state)
    {
        if (string.IsNullOrWhiteSpace(credentialReference))
        {
            return CredentialUseDecision.NoCredential;
        }

        return state switch
        {
            CredentialState.Revoked => CredentialUseDecision.Revoked,
            CredentialState.Expired => CredentialUseDecision.Expired,
            _ => CredentialUseDecision.Usable,
        };
    }
}
