using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Acceptance scenario #21 (requirements A7 §9.3 and §9.5): a forged provider callback is rejected
/// and never processed, and Staff cannot reach credential data or the Owner integration controls.
/// </summary>
/// <remarks>
/// The host runs in-process against real SQL Server built from the actual migrations. The Phase A
/// signature verifier is the disabled default, so every callback is forged from the endpoint's
/// point of view; the test also seeds a matching connection so the rejection is attributable to
/// the signature gate rather than to a missing connection.
/// </remarks>
[Collection(AcceptanceApiTestGroup.Name)]
public sealed class IntegrationAccessAcceptanceTests
{
    private const string CsrfHeaderName = "X-CSRF-TOKEN";
    private const string IntegrationsRoot = "/api/v1/integrations";

    private readonly AcceptanceApiFactory _factory;

    public IntegrationAccessAcceptanceTests(AcceptanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgedProviderCallback_IsRejectedAndNothingIsRecordedInInboxAsync()
    {
        // Arrange: a connection exists for the provider, so only the signature gate can reject.
        await SeedConnectionAsync("Square");
        long inboxBefore = await CountInboxAsync();
        using HttpClient anonymous = _factory.CreateApiClient();

        // Act: a callback with a bogus signature and a fabricated delivery id.
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/integrations/webhooks/square")
        {
            Content = new StringContent("{\"event\":\"forged\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Signature", "sha256=deadbeef");
        request.Headers.Add("X-Delivery-Id", $"forged-{Guid.NewGuid():N}");
        using HttpResponseMessage response = await anonymous.SendAsync(request);

        // Assert: refused as unauthenticated, and no inbox row was written.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(inboxBefore, await CountInboxAsync());
    }

    [Theory]
    [InlineData("/connections")]
    [InlineData("/mappings")]
    [InlineData("/sync-runs")]
    [InlineData("/exceptions")]
    public async Task IntegrationManagement_AsStaff_IsForbiddenAsync(string path)
    {
        using HttpClient staff = await AcceptanceApiSession.AuthenticateNewStaffAsync(
            _factory,
            $"staff-{Guid.NewGuid():N}@penguinplank.test",
            "Staff!Password#2025x");

        using HttpResponseMessage response = await staff.GetAsync(IntegrationsRoot + path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IntegrationControls_AsStaff_MutationIsForbiddenAsync()
    {
        Guid connectionId = await SeedConnectionAsync("Shopify");
        using HttpClient staff = await AcceptanceApiSession.AuthenticateNewStaffAsync(
            _factory,
            $"staff-{Guid.NewGuid():N}@penguinplank.test",
            "Staff!Password#2025x");
        string csrf = await FetchCsrfTokenAsync(staff);

        using HttpRequestMessage request = new(HttpMethod.Post, $"{IntegrationsRoot}/connections/{connectionId}/pause");
        request.Headers.Add(CsrfHeaderName, csrf);
        using HttpResponseMessage response = await staff.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using PenguinPlankDbContext context = _factory.CreateContext();
        IntegrationConnection stored = await context.IntegrationConnections.AsNoTracking()
            .SingleAsync(connection => connection.Id == connectionId);
        Assert.False(stored.IsPaused);
    }

    [Theory]
    [InlineData("/connections")]
    [InlineData("/mappings")]
    [InlineData("/sync-runs")]
    [InlineData("/exceptions")]
    public async Task IntegrationManagement_Anonymous_IsDeniedAsync(string path)
    {
        using HttpClient anonymous = _factory.CreateApiClient();

        using HttpResponseMessage response = await anonymous.GetAsync(IntegrationsRoot + path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> SeedConnectionAsync(string platform)
    {
        await using PenguinPlankDbContext context = _factory.CreateContext();
        IntegrationConnection connection = new()
        {
            Id = Guid.NewGuid(),
            Platform = platform,
            Account = $"acct-{Guid.NewGuid():N}",
            Scopes = "read",
            ApiVersion = "v1",
            EnabledCapabilities = "orders",
            CredentialReference = "vault://test/secret",
        };
        context.IntegrationConnections.Add(connection);
        await context.SaveChangesAsync();
        return connection.Id;
    }

    private async Task<long> CountInboxAsync()
    {
        await using PenguinPlankDbContext context = _factory.CreateContext();
        return await context.IntegrationInbox.LongCountAsync();
    }

    private static async Task<string> FetchCsrfTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("No CSRF token returned.");
    }
}
