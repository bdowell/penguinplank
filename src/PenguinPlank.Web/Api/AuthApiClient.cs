using PenguinPlank.Contracts.Authentication;

namespace PenguinPlank.Web.Api;

/// <summary>Default <see cref="IAuthApiClient"/> over <see cref="ApiRequestSender"/>.</summary>
public sealed class AuthApiClient : IAuthApiClient
{
    private const string LoginPath = "api/v1/auth/login";
    private const string LogoutPath = "api/v1/auth/logout";
    private const string CurrentActorPath = "api/v1/auth/me";

    private readonly ApiRequestSender _sender;

    /// <summary>Initializes the client.</summary>
    /// <param name="sender">The request sender.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sender"/> is <see langword="null"/>.</exception>
    public AuthApiClient(ApiRequestSender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task<ApiResult<CurrentActorResponse>> LoginAsync(LoginContract login, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        ApiResult<CurrentActorResponse> result = await _sender
            .SendAsync<LoginContract, CurrentActorResponse>(HttpMethod.Post, LoginPath, login, ifMatch: null, idempotencyKey: null, cancellationToken)
            .ConfigureAwait(false);

        // Signing in changes the identity the antiforgery token is bound to.
        _sender.InvalidateCsrfToken();
        return result;
    }

    /// <inheritdoc />
    public async Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        ApiResult result = await _sender.SendAsync(HttpMethod.Post, LogoutPath, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _sender.InvalidateCsrfToken();
        }

        return result;
    }

    /// <inheritdoc />
    public Task<ApiResult<CurrentActorResponse>> GetCurrentActorAsync(CancellationToken cancellationToken = default) =>
        _sender.GetAsync<CurrentActorResponse>(CurrentActorPath, cancellationToken);
}
