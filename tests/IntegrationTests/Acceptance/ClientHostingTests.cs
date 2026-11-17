using System.Net;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Task 17.6: the API hosts the Blazor client with an <c>index.html</c> fallback for client routes,
/// but the fallback never answers <c>/api</c> paths and the authorization fallback policy is
/// unchanged.
/// </summary>
/// <remarks>
/// ASP.NET Core applies the fallback authorization policy to requests that match no endpoint, so an
/// anonymous request to an unknown <c>/api</c> path is challenged with 401 (as it was before the
/// client was hosted). An authenticated request to the same path is a plain 404. Neither returns the
/// client shell.
/// </remarks>
[Collection(AcceptanceApiTestGroup.Name)]
public sealed class ClientHostingTests
{
    private readonly AcceptanceApiFactory _factory;

    public ClientHostingTests(AcceptanceApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/api/unknown")]
    [InlineData("/api")]
    public async Task GetAsync_UnknownApiPathAuthenticated_Returns404WithoutClientShellAsync(string path)
    {
        using HttpClient owner = await AcceptanceApiSession.AuthenticateOwnerAsync(_factory);

        using HttpResponseMessage response = await owner.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNotClientShellAsync(response);
    }

    [Theory]
    [InlineData("/api/v1/does-not-exist")]
    [InlineData("/api/unknown")]
    public async Task GetAsync_UnknownApiPathAnonymous_IsChallengedWithoutClientShellAsync(string path)
    {
        using HttpClient anonymous = _factory.CreateApiClient();

        using HttpResponseMessage response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNotClientShellAsync(response);
    }

    private static async Task AssertNotClientShellAsync(HttpResponseMessage response)
    {
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }
}
