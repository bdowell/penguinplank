using PenguinPlank.Contracts.Authentication;
using PenguinPlank.Web.Api;
using PenguinPlank.Web.Shell;

namespace PenguinPlank.UnitTests.Web;

public sealed class ShellTests
{
    private static readonly ApiError s_unauthenticated = new(ApiErrorKind.Unauthenticated, 401, null, "Unauthorized", null, null);
    private static readonly ApiError s_offline = new(ApiErrorKind.Connectivity, null, null, "Offline", null, null);

    private sealed class FakeAuthApiClient : IAuthApiClient
    {
        public ApiResult<CurrentActorResponse> MeResult { get; set; } = ApiResult.Failure<CurrentActorResponse>(s_unauthenticated);

        public ApiResult<CurrentActorResponse> LoginResult { get; set; } = ApiResult.Failure<CurrentActorResponse>(s_unauthenticated);

        public ApiResult LogoutResult { get; set; } = ApiResult.Success();

        public int MeCalls { get; private set; }

        public LoginContract? LastLogin { get; private set; }

        public Task<ApiResult<CurrentActorResponse>> LoginAsync(LoginContract login, CancellationToken cancellationToken = default)
        {
            LastLogin = login;
            return Task.FromResult(LoginResult);
        }

        public Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(LogoutResult);

        public Task<ApiResult<CurrentActorResponse>> GetCurrentActorAsync(CancellationToken cancellationToken = default)
        {
            MeCalls++;
            return Task.FromResult(MeResult);
        }
    }

    private static ApiResult<CurrentActorResponse> Actor(string role) =>
        ApiResult.Success(new CurrentActorResponse { UserId = Guid.NewGuid(), Email = "a@example.com", Role = role });

    [Fact]
    public void GetItems_Owner_IncludesIntegrationsAndSettings()
    {
        string[] labels = [.. NavigationCatalog.GetItems("Owner").Select(i => i.Label)];

        Assert.Equal(
            ["Dashboard", "Products & Stock", "Workshop", "Markets", "Costing", "Photos & Listings", "Wholesale", "Integrations", "Settings"],
            labels);
    }

    [Fact]
    public void GetItems_Staff_HidesOwnerOnlyEntries()
    {
        string[] labels = [.. NavigationCatalog.GetItems("Staff").Select(i => i.Label)];

        Assert.Contains("Products & Stock", labels);
        Assert.DoesNotContain("Integrations", labels);
        Assert.DoesNotContain("Settings", labels);
    }

    [Fact]
    public void GetItems_NoRole_ReturnsNothing()
    {
        Assert.Empty(NavigationCatalog.GetItems(null));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("/catalog/products", "catalog/products")]
    [InlineData("/catalog/products?page=2", "catalog/products?page=2")]
    [InlineData("https://evil.example/", "")]
    [InlineData("//evil.example/", "")]
    [InlineData("/\\evil.example", "")]
    [InlineData("/login", "")]
    [InlineData("/login?returnUrl=%2F", "")]
    public void Resolve_Candidate_AllowsOnlySameOriginRelativePaths(string? candidate, string expected)
    {
        Assert.Equal(expected, ReturnUrlPolicy.Resolve(candidate));
    }

    [Theory]
    [InlineData("", "login")]
    [InlineData("catalog/products", "login?returnUrl=%2Fcatalog%2Fproducts")]
    [InlineData("login", "login")]
    public void BuildLoginUrl_CurrentLocation_CarriesSafeReturnUrl(string current, string expected)
    {
        Assert.Equal(expected, ReturnUrlPolicy.BuildLoginUrl(current));
    }

    [Fact]
    public async Task EnsureLoadedAsync_ServerReportsActor_BecomesAuthenticatedAsync()
    {
        var client = new FakeAuthApiClient { MeResult = Actor("Owner") };
        var session = new AuthSession(client);

        await session.EnsureLoadedAsync();
        await session.EnsureLoadedAsync();

        Assert.Equal(SessionStatus.Authenticated, session.Status);
        Assert.True(session.IsOwner);
        Assert.Equal(1, client.MeCalls);
    }

    [Fact]
    public async Task EnsureLoadedAsync_Unauthenticated_BecomesAnonymousAsync()
    {
        var session = new AuthSession(new FakeAuthApiClient());

        await session.EnsureLoadedAsync();

        Assert.Equal(SessionStatus.Anonymous, session.Status);
        Assert.Null(session.Actor);
    }

    [Fact]
    public async Task EnsureLoadedAsync_ConnectivityLoss_IsUnavailableAndRetriesAsync()
    {
        var client = new FakeAuthApiClient { MeResult = ApiResult.Failure<CurrentActorResponse>(s_offline) };
        var session = new AuthSession(client);

        await session.EnsureLoadedAsync();
        await session.EnsureLoadedAsync();

        Assert.Equal(SessionStatus.Unavailable, session.Status);
        Assert.Equal(2, client.MeCalls);
    }

    [Theory]
    [InlineData("", "pw")]
    [InlineData("a@example.com", "")]
    [InlineData("  ", "pw")]
    public async Task SignInAsync_MissingField_RejectsWithoutCallingApiAsync(string email, string password)
    {
        var client = new FakeAuthApiClient();
        var session = new AuthSession(client);

        SignInOutcome outcome = await session.SignInAsync(email, password);

        Assert.False(outcome.Succeeded);
        Assert.Null(client.LastLogin);
    }

    [Fact]
    public async Task SignInAsync_ValidCredentials_AdoptsActorAndTrimsEmailAsync()
    {
        var client = new FakeAuthApiClient { LoginResult = Actor("Staff") };
        var session = new AuthSession(client);

        SignInOutcome outcome = await session.SignInAsync(" a@example.com ", "pw");

        Assert.True(outcome.Succeeded);
        Assert.Equal("a@example.com", client.LastLogin!.Email);
        Assert.Equal(SessionStatus.Authenticated, session.Status);
        Assert.False(session.IsOwner);
    }

    [Fact]
    public async Task SignInAsync_BadCredentials_StaysAnonymousWithGenericMessageAsync()
    {
        var session = new AuthSession(new FakeAuthApiClient());

        SignInOutcome outcome = await session.SignInAsync("a@example.com", "wrong");

        Assert.False(outcome.Succeeded);
        Assert.Contains("incorrect", outcome.Message, StringComparison.Ordinal);
        Assert.NotEqual(SessionStatus.Authenticated, session.Status);
    }

    [Fact]
    public async Task SignOutAsync_ServerFailure_KeepsSessionAsync()
    {
        var client = new FakeAuthApiClient { LoginResult = Actor("Owner"), LogoutResult = ApiResult.Failure(s_offline) };
        var session = new AuthSession(client);
        await session.SignInAsync("a@example.com", "pw");

        ApiResult result = await session.SignOutAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(SessionStatus.Authenticated, session.Status);
    }

    [Fact]
    public async Task SignOutAsync_Success_ClearsActorAndRaisesChangedAsync()
    {
        var client = new FakeAuthApiClient { LoginResult = Actor("Owner") };
        var session = new AuthSession(client);
        await session.SignInAsync("a@example.com", "pw");
        int changes = 0;
        session.Changed += () => changes++;

        await session.SignOutAsync();

        Assert.Equal(SessionStatus.Anonymous, session.Status);
        Assert.Null(session.Actor);
        Assert.Equal(1, changes);
    }
}
