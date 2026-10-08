namespace PenguinPlank.Application.IdentityAdministration.Authentication;

/// <summary>
/// The operator-supplied credentials for a staff sign-in attempt (requirement A5 §7.1).
/// </summary>
/// <remarks>
/// <para>
/// An immutable value object carried from the login endpoint into the authentication abstraction.
/// Phase A authenticates <b>staff accounts only</b> and offers no public self-registration
/// (requirement A2 §5.9), so this request is a credential-check input, never an account-creation
/// one. The secret it carries is never logged or echoed back; it flows into the sign-in boundary
/// and no further.
/// </para>
/// </remarks>
public sealed record SignInRequest
{
    /// <summary>Creates a sign-in request from the supplied email and password.</summary>
    /// <param name="email">The staff member's email (and username).</param>
    /// <param name="password">The staff member's password.</param>
    public SignInRequest(string? email, string? password)
    {
        Email = email;
        Password = password;
    }

    /// <summary>The staff member's email (and username).</summary>
    public string? Email { get; }

    /// <summary>The staff member's password. Never logged or returned to a caller.</summary>
    public string? Password { get; }
}
