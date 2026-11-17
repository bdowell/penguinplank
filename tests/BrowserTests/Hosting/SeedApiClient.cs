using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PenguinPlank.Contracts.Catalog;

namespace BrowserTests.Hosting;

/// <summary>
/// Arranges and inspects server state through the real HTTP API, outside the browser under test.
/// It signs in as the first Owner through the genuine bootstrap, CSRF, and login endpoints (the
/// same flow the integration suite uses), then creates the Staff accounts, products, and variants
/// a scenario needs and reads a variant back so a test can assert what is actually persisted.
/// </summary>
/// <remarks>
/// The Web client has no create screens in Phase A, so creation is arranged here; every edit the
/// tests care about is made through the browser UI.
/// </remarks>
public sealed class SeedApiClient : IDisposable
{
    private const string CsrfHeaderName = "X-CSRF-TOKEN";
    private const string IdempotencyHeaderName = "Idempotency-Key";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    private SeedApiClient(HttpClient client)
    {
        _client = client;
    }

    /// <summary>Provisions the first Owner (idempotent) and returns a client signed in as that Owner.</summary>
    /// <param name="factory">The running API host.</param>
    /// <returns>An authenticated seed client.</returns>
    public static async Task<SeedApiClient> SignInOwnerAsync(BrowserApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // https so the hardened (Secure) session cookie is accepted and re-sent by the cookie jar.
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        try
        {
            using HttpRequestMessage bootstrap = new(HttpMethod.Post, "/api/v1/owner-bootstrap");
            bootstrap.Headers.Add(CsrfHeaderName, await FetchCsrfTokenAsync(client));
            await SendExpectingSuccessAsync(client, bootstrap, "owner bootstrap");

            using HttpRequestMessage login = new(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    email = BrowserApiFactory.OwnerEmail,
                    password = BrowserApiFactory.OwnerPassword,
                }),
            };
            login.Headers.Add(CsrfHeaderName, await FetchCsrfTokenAsync(client));
            await SendExpectingSuccessAsync(client, login, "owner login");
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new SeedApiClient(client);
    }

    /// <summary>Creates a Staff account through the Owner-only users endpoint.</summary>
    /// <param name="email">The new account's email.</param>
    /// <param name="password">The new account's password (must satisfy the Identity policy).</param>
    /// <returns>A task that completes when the account exists.</returns>
    public async Task CreateStaffAsync(string email, string password)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(new { email, password, role = "Staff" }),
        };
        request.Headers.Add(CsrfHeaderName, await FetchCsrfTokenAsync(_client));
        await SendExpectingSuccessAsync(_client, request, "create staff user");
    }

    /// <summary>Creates a product.</summary>
    /// <param name="name">The product name.</param>
    /// <returns>The new product's id.</returns>
    public async Task<Guid> CreateProductAsync(string name)
    {
        return await PostCreateAsync("/api/v1/products", new CreateProductContract { Name = name }, "create product");
    }

    /// <summary>Creates a quantity-tracked variant with a retail price under a product.</summary>
    /// <param name="productId">The owning product.</param>
    /// <param name="sku">The unique SKU.</param>
    /// <returns>The new variant's id.</returns>
    public async Task<Guid> CreateVariantAsync(Guid productId, string sku)
    {
        CreateVariantContract contract = new()
        {
            ProductId = productId,
            Sku = sku,
            TrackingMode = "Quantity",
            UnitOfMeasure = "each",
            RetailPrice = 49.99m,
        };

        return await PostCreateAsync("/api/v1/variants", contract, "create variant");
    }

    /// <summary>Reads a variant as the signed-in Owner.</summary>
    /// <param name="variantId">The variant to read.</param>
    /// <returns>The persisted variant.</returns>
    public async Task<VariantResponse> GetVariantAsync(Guid variantId)
    {
        using HttpResponseMessage response = await _client.GetAsync($"/api/v1/variants/{variantId}");
        await EnsureSuccessAsync(response, "read variant");

        return await response.Content.ReadFromJsonAsync<VariantResponse>(s_json)
            ?? throw new InvalidOperationException("The variant response was empty.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<Guid> PostCreateAsync<TContract>(string path, TContract contract, string step)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(contract, options: s_json),
        };
        request.Headers.Add(IdempotencyHeaderName, Guid.NewGuid().ToString());

        using HttpResponseMessage response = await _client.SendAsync(request);
        await EnsureSuccessAsync(response, step);

        await using Stream body = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<string> FetchCsrfTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/csrf");
        await EnsureSuccessAsync(response, "csrf token");

        await using Stream body = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(body);
        return document.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The CSRF endpoint returned no request token.");
    }

    private static async Task SendExpectingSuccessAsync(HttpClient client, HttpRequestMessage request, string step)
    {
        using HttpResponseMessage response = await client.SendAsync(request);
        await EnsureSuccessAsync(response, step);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string step)
    {
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.NoContent)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"The arrange step '{step}' failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
    }
}
