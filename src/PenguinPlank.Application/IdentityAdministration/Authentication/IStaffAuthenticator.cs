using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.IdentityAdministration.Authentication;

/// <summary>
/// The narrow authentication boundary for a staff session: establish a session from credentials,
/// end it, and describe the current authenticated actor (requirement A5 §7.1, §7.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this abstraction exists.</b> Phase A implements browser authentication with a secure,
/// HttpOnly, same-origin cookie (requirement A2 §5.? / A5 §7.2); there is no bearer token in
/// browser storage. A future mobile client is intended to use OIDC authorization-code flow with
/// PKCE under the same staff policies (requirement A5 §7.3). Keeping sign-in / sign-out /
/// current-actor behind this focused, responsibility-named interface (coding-standards §2) means
/// the login and logout endpoints depend only on the abstraction: the Phase A cookie
/// implementation can be replaced by an OIDC-backed one when the mobile client is in scope, with
/// no change to the callers. It is deliberately <b>not</b> a broad <c>IAuthManager</c>; it names
/// exactly the three session operations the endpoints need.
/// </para>
/// <para>
/// <b>Boundary discipline.</b> The implementation lives in Infrastructure/Api because establishing
/// a cookie session is inseparable from the ASP.NET Core Identity sign-in framework. It returns the
/// role-safe <see cref="AuthenticatedActor"/> — never a framework principal, an Identity entity, or
/// a credential. Expected failures (unknown account, wrong password, locked out) are modeled as a
/// typed <see cref="Result"/> carrying <see cref="ErrorCode.Validation"/>, not as an exception; a
/// uniform failure avoids disclosing which accounts exist. Every method takes and propagates a
/// <see cref="CancellationToken"/>.
/// </para>
/// </remarks>
public interface IStaffAuthenticator
{
    /// <summary>
    /// Validates the supplied staff credentials and, on success, establishes the session (the
    /// Phase A cookie).
    /// </summary>
    /// <param name="request">The supplied email and password.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the authenticated actor, or a <see cref="ErrorCode.Validation"/> failure
    /// with a uniform message when the credentials are not accepted.
    /// </returns>
    Task<Result<AuthenticatedActor>> SignInAsync(SignInRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the current session (clears the Phase A cookie). Safe to call when no session exists.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the session has been ended.</returns>
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Describes the current authenticated actor for the request, or <see langword="null"/> when no
    /// staff session is established.
    /// </summary>
    /// <returns>The role-safe current actor, or <see langword="null"/> when unauthenticated.</returns>
    AuthenticatedActor? GetCurrentActor();
}
