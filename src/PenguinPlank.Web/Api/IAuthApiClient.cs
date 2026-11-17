using PenguinPlank.Contracts.Authentication;

namespace PenguinPlank.Web.Api;

/// <summary>Typed client for the <c>/api/v1/auth</c> session endpoints.</summary>
public interface IAuthApiClient
{
    /// <summary>
    /// Signs in. On success the server sets the HttpOnly session cookie; no token is returned.
    /// </summary>
    /// <param name="login">The credentials.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The signed-in actor, or a typed failure (401 maps to <see cref="ApiErrorKind.Unauthenticated"/>).</returns>
    Task<ApiResult<CurrentActorResponse>> LoginAsync(LoginContract login, CancellationToken cancellationToken = default);

    /// <summary>Signs out and clears the session cookie.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success or a typed failure.</returns>
    Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the actor of the current session.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The actor, or <see cref="ApiErrorKind.Unauthenticated"/> when there is no session.</returns>
    Task<ApiResult<CurrentActorResponse>> GetCurrentActorAsync(CancellationToken cancellationToken = default);
}
