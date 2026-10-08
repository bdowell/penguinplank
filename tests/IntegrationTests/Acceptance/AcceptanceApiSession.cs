using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Drives the <b>real</b> authentication surface for the acceptance tests: it provisions the first
/// Owner through the one-time bootstrap endpoint, logs an actor in through <c>/auth/login</c>
/// (handling the CSRF token from <c>/auth/csrf</c>), and provisions a Staff account through the
/// Owner-only <c>/users</c> endpoint. Each helper returns a cookie-bearing <see cref="HttpClient"/>
/// already authenticated as the requested actor, so a scenario arranges its callers through the
/// genuine Identity flow rather than a test shortcut.
/// </summary>
/// <remarks>
/// The browser auth is a secure HttpOnly same-origin cookie with CSRF on mutations, so each sign-in
/// first fetches a CSRF token and echoes it in the configured header on the login POST; the cookie
/// container on the client then carries the session for subsequent reads. Keeping this flow in one
/// small helper keeps each test's arrange step explicit and short (coding-standards §7).
/// </remarks>
internal static class AcceptanceApiSession
{
    private const string CsrfHeaderName = "X-CSRF-TOKEN";

    private static readonly JsonSerializerOptions s_json =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Provisions the first Owner through the one-time bootstrap endpoint (idempotent) and returns
    /// a client authenticated as that Owner.
    /// </summary>
    public static async Task<HttpClient> AuthenticateOwnerAsync(AcceptanceApiFactory factory)
    {
        System.ArgumentNullException.ThrowIfNull(factory);

        HttpClient client = factory.CreateApiClient();

        // The one-time Owner bootstrap is anonymous and idempotent; it creates the Owner from the
        // operator-supplied configuration the factory injected. A repeat call is a harmless no-op.
        // It is a mutating POST, so the antiforgery middleware still requires a CSRF token even
        // though the endpoint allows anonymous callers; fetch and echo the token like any mutation.
        string bootstrapCsrf = await FetchCsrfTokenAsync(client);
        using HttpRequestMessage bootstrapRequest = new(HttpMethod.Post, "/api/v1/owner-bootstrap");
        bootstrapRequest.Headers.Add(CsrfHeaderName, bootstrapCsrf);
        using HttpResponseMessage bootstrap = await client.SendAsync(bootstrapRequest);
        EnsureSuccess(bootstrap, "owner-bootstrap");

        await SignInAsync(client, AcceptanceApiFactory.OwnerEmail, AcceptanceApiFactory.OwnerPassword);
        return client;
    }

    /// <summary>
    /// Ensures the first Owner exists, provisions a Staff account through the Owner-only
    /// <c>/users</c> endpoint, and returns a client authenticated as that new Staff member.
    /// </summary>
    public static async Task<HttpClient> AuthenticateNewStaffAsync(
        AcceptanceApiFactory factory,
        string email,
        string password)
    {
        System.ArgumentNullException.ThrowIfNull(factory);

        // Create the Staff account as the Owner (the only account-creation surface).
        HttpClient ownerClient = await AuthenticateOwnerAsync(factory);
        using (ownerClient)
        {
            string csrf = await FetchCsrfTokenAsync(ownerClient);
            using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/users")
            {
                Content = JsonContent.Create(new
                {
                    email,
                    password,
                    role = "Staff",
                }),
            };
            request.Headers.Add(CsrfHeaderName, csrf);

            using HttpResponseMessage created = await ownerClient.SendAsync(request);
            EnsureSuccess(created, "create staff user");
        }

        // Sign the new Staff member in on a fresh client with its own cookie container.
        HttpClient staffClient = factory.CreateApiClient();
        await SignInAsync(staffClient, email, password);
        return staffClient;
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        string csrf = await FetchCsrfTokenAsync(client);

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password }),
        };
        request.Headers.Add(CsrfHeaderName, csrf);

        using HttpResponseMessage response = await client.SendAsync(request);
        EnsureSuccess(response, "login");
    }

    private static async Task<string> FetchCsrfTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/csrf");
        EnsureSuccess(response, "csrf token");

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The CSRF endpoint returned no request token.");
    }

    private static void EnsureSuccess(HttpResponseMessage response, string step)
    {
        if (response.StatusCode is HttpStatusCode.OK
            or HttpStatusCode.Created
            or HttpStatusCode.NoContent)
        {
            return;
        }

        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        throw new InvalidOperationException(
            $"The acceptance auth step '{step}' failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
    }

    /// <summary>The shared Web-defaults JSON options used to deserialize API responses in tests.</summary>
    public static JsonSerializerOptions Json => s_json;
}
