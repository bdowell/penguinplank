using PenguinPlank.Contracts.Authentication;
using PenguinPlank.Web.Api;

namespace PenguinPlank.Web.Shell;

/// <summary>
/// Holds the browser's view of the HttpOnly cookie session. The actor comes from the server's
/// <c>/auth/me</c> endpoint; no credential or token is kept in browser storage (requirement A5 7.1).
/// </summary>
public sealed class AuthSession
{
    private readonly IAuthApiClient _authApiClient;

    /// <summary>Initializes the session holder.</summary>
    /// <param name="authApiClient">The typed auth client.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="authApiClient"/> is <see langword="null"/>.</exception>
    public AuthSession(IAuthApiClient authApiClient)
    {
        ArgumentNullException.ThrowIfNull(authApiClient);
        _authApiClient = authApiClient;
    }

    /// <summary>Raised after <see cref="Status"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Gets the current session status.</summary>
    public SessionStatus Status { get; private set; } = SessionStatus.Unknown;

    /// <summary>Gets the authenticated actor, or <see langword="null"/> when not authenticated.</summary>
    public CurrentActorResponse? Actor { get; private set; }

    /// <summary>Gets a value indicating whether the actor is the owner.</summary>
    public bool IsOwner => Actor is not null && NavigationCatalog.IsOwner(Actor.Role);

    /// <summary>Checks the session with the server unless a definite answer is already known.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the status is known.</returns>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        Status is SessionStatus.Unknown or SessionStatus.Unavailable
            ? RefreshAsync(cancellationToken)
            : Task.CompletedTask;

    /// <summary>Re-reads the session from the server.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the status is updated.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ApiResult<CurrentActorResponse> result = await _authApiClient
            .GetCurrentActorAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            Update(SessionStatus.Authenticated, result.Value);
        }
        else if (result.Error?.Kind == ApiErrorKind.Unauthenticated)
        {
            Update(SessionStatus.Anonymous, actor: null);
        }
        else
        {
            Update(SessionStatus.Unavailable, actor: null);
        }
    }

    /// <summary>Signs in and, on success, adopts the returned actor.</summary>
    /// <param name="email">The email entered by the user.</param>
    /// <param name="password">The password entered by the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with a caller-safe message on failure.</returns>
    public async Task<SignInOutcome> SignInAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return SignInOutcome.Failure("Enter your email and password.");
        }

        ApiResult<CurrentActorResponse> result = await _authApiClient
            .LoginAsync(new LoginContract { Email = email.Trim(), Password = password }, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            Update(SessionStatus.Authenticated, result.Value);
            return SignInOutcome.Success;
        }

        return SignInOutcome.Failure(DescribeSignInFailure(result.Error));
    }

    /// <summary>Signs out. The local state changes only when the server confirms.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The server result, so the caller can report a failure.</returns>
    public async Task<ApiResult> SignOutAsync(CancellationToken cancellationToken = default)
    {
        ApiResult result = await _authApiClient.LogoutAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            Update(SessionStatus.Anonymous, actor: null);
        }

        return result;
    }

    /// <summary>Maps a sign-in failure to a message that does not reveal which credential was wrong.</summary>
    /// <param name="error">The failure, if any.</param>
    /// <returns>The message to display.</returns>
    public static string DescribeSignInFailure(ApiError? error) => error?.Kind switch
    {
        ApiErrorKind.Unauthenticated or ApiErrorKind.Validation or ApiErrorKind.Forbidden =>
            "The email or password is incorrect, or the account is locked.",
        ApiErrorKind.RateLimited => "Too many attempts. Wait a moment and try again.",
        ApiErrorKind.Connectivity => "Cannot reach the server. Check your connection and try again.",
        _ => "Sign-in failed because of a server problem. Try again shortly.",
    };

    private void Update(SessionStatus status, CurrentActorResponse? actor)
    {
        Status = status;
        Actor = actor;
        Changed?.Invoke();
    }
}
