using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Authentication;
using PenguinPlank.Domain.Common;

namespace UnitTests.IdentityAdministration.Authentication;

/// <summary>
/// Unit tests for the browser authentication <b>abstraction</b> (task 9.3): the role-safe
/// <see cref="AuthenticatedActor"/> and <see cref="SignInRequest"/> value objects, and the
/// <see cref="IStaffAuthenticator"/> session contract exercised through a hand-written in-memory
/// implementation.
/// </summary>
/// <remarks>
/// The Phase A cookie implementation (<c>CookieStaffAuthenticator</c>) is framework glue over
/// ASP.NET Core <c>SignInManager</c> and <c>HttpContext</c>; its behavior is covered by the endpoint
/// property/integration tests (tasks 9.5/9.6). These tests pin the <em>abstraction</em> the
/// login/logout endpoints depend on — the shapes that cross the boundary and the sign-in /
/// sign-out / current-actor semantics — so a future OIDC+PKCE implementation (requirement A5 §7.3)
/// has an explicit contract to satisfy. No framework, SQL, network, or real clock is involved.
/// </remarks>
public sealed class StaffAuthenticationContractTests
{
    /// <summary>
    /// A hand-written in-memory <see cref="IStaffAuthenticator"/>: it validates credentials against
    /// a known account, tracks whether a session is established, and reports the current actor. It
    /// models the contract both the cookie and a future OIDC implementation must satisfy.
    /// </summary>
    private sealed class InMemoryStaffAuthenticator : IStaffAuthenticator
    {
        private readonly AuthenticatedActor _knownActor;
        private readonly string _knownPassword;
        private bool _signedIn;

        public InMemoryStaffAuthenticator(AuthenticatedActor knownActor, string knownPassword)
        {
            _knownActor = knownActor;
            _knownPassword = knownPassword;
        }

        public Task<Result<AuthenticatedActor>> SignInAsync(SignInRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool matches = string.Equals(request.Email, _knownActor.Email, StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Password, _knownPassword, StringComparison.Ordinal);

            if (!matches)
            {
                return Task.FromResult(
                    Result.Failure<AuthenticatedActor>(ErrorCode.Validation, "The email or password is incorrect."));
            }

            _signedIn = true;
            return Task.FromResult(Result.Success(_knownActor));
        }

        public Task SignOutAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _signedIn = false;
            return Task.CompletedTask;
        }

        public AuthenticatedActor? GetCurrentActor() => _signedIn ? _knownActor : null;
    }

    private static readonly AuthenticatedActor s_owner =
        new(new Guid("66666666-6666-6666-6666-666666666666"), "owner@penguinplank.test", Role.Owner);

    [Fact]
    public async Task SignIn_WithValidCredentials_SucceedsAndEstablishesCurrentActorAsync()
    {
        var authenticator = new InMemoryStaffAuthenticator(s_owner, "Zr7!qvW2m#Lx");

        Result<AuthenticatedActor> result = await authenticator.SignInAsync(
            new SignInRequest("owner@penguinplank.test", "Zr7!qvW2m#Lx"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Role.Owner, result.Value.Role);
        Assert.NotNull(authenticator.GetCurrentActor());
        Assert.Equal(s_owner.UserId, authenticator.GetCurrentActor()!.UserId);
    }

    [Theory]
    [InlineData("owner@penguinplank.test", "wrong-password")]
    [InlineData("unknown@penguinplank.test", "Zr7!qvW2m#Lx")]
    public async Task SignIn_WithWrongCredentials_FailsUniformlyAndLeavesNoSessionAsync(string email, string password)
    {
        var authenticator = new InMemoryStaffAuthenticator(s_owner, "Zr7!qvW2m#Lx");

        Result<AuthenticatedActor> result = await authenticator.SignInAsync(
            new SignInRequest(email, password), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        // The failure message is uniform — it does not disclose whether the account exists.
        Assert.Equal("The email or password is incorrect.", result.Error.Message);
        Assert.Null(authenticator.GetCurrentActor());
    }

    [Fact]
    public async Task SignOut_AfterSignIn_ClearsTheCurrentActorAsync()
    {
        var authenticator = new InMemoryStaffAuthenticator(s_owner, "Zr7!qvW2m#Lx");
        await authenticator.SignInAsync(new SignInRequest("owner@penguinplank.test", "Zr7!qvW2m#Lx"), CancellationToken.None);

        await authenticator.SignOutAsync(CancellationToken.None);

        Assert.Null(authenticator.GetCurrentActor());
    }

    [Fact]
    public void AuthenticatedActor_WithEmptyUserId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new AuthenticatedActor(Guid.Empty, "x@y.test", Role.Staff));
    }

    [Fact]
    public void SignInRequest_CarriesSuppliedCredentials()
    {
        var request = new SignInRequest("a@b.test", "secret");

        Assert.Equal("a@b.test", request.Email);
        Assert.Equal("secret", request.Password);
    }
}
