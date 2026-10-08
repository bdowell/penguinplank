using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Acceptance scenario #10 (requirement A2 §5.11): a Staff-authenticated caller cannot obtain unit
/// cost, wholesale cost, margin, or profit through <b>any</b> catalog endpoint or field.
/// </summary>
/// <remarks>
/// <para>
/// This exercises the real HTTP pipeline end-to-end: an Owner creates a product and a variant
/// carrying pricing (the role-safe <em>sale</em> prices plus the SKU/wood/sizing public fields),
/// then a genuinely signed-in Staff member reads the variant list and detail. The assertion is
/// <b>structural</b>: no property whose name mentions cost, margin, or profit appears anywhere in
/// the Staff JSON, while the role-safe fields (SKU, retail/wholesale sale prices) are present. A
/// positive control confirms the Owner can read the same resource and receives the role-safe fields
/// — so the Staff exclusion is a field-level allowlist, not a blanket denial (requirement A2
/// §5.10, §5.11; design Property 11).
/// </para>
/// <para>
/// The host runs against a real isolated SQL Server database built from the actual migrations
/// (see <see cref="AcceptanceApiFactory"/>), never EF InMemory, so the read path is the production
/// reader/projector over real relational data.
/// </para>
/// </remarks>
[Collection(AcceptanceApiTestGroup.Name)]
public sealed class StaffFinancialExclusionAcceptanceTests
{
    // Field-name fragments that would betray Owner-only financial data if they ever surfaced to a
    // Staff caller. "wholesale cost" is distinct from the role-safe "wholesalePrice" sale price.
    private static readonly string[] s_forbiddenFinancialFragments =
    [
        "cost",
        "margin",
        "profit",
        "markup",
    ];

    private readonly AcceptanceApiFactory _factory;

    public StaffFinancialExclusionAcceptanceTests(AcceptanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetVariant_AsStaff_ExposesNoCostMarginOrProfitFieldWhileOwnerCanReadItAsync()
    {
        // Arrange: the Owner creates a product and a variant that carries pricing (sale prices) and
        // the public SKU/sizing fields — the catalog data a Staff caller is allowed to see, minus
        // any Owner-only financial figure.
        using HttpClient ownerClient = await AcceptanceApiSession.AuthenticateOwnerAsync(_factory);
        Guid productId = await CreateProductAsync(ownerClient);
        (Guid variantId, string sku) = await CreateVariantAsync(ownerClient, productId);

        string staffEmail = $"staff-{Guid.NewGuid():N}@penguinplank.test";
        const string staffPassword = "Staff!Member#2025";
        using HttpClient staffClient =
            await AcceptanceApiSession.AuthenticateNewStaffAsync(_factory, staffEmail, staffPassword);

        // Act: Staff reads the variant through BOTH read paths — the list and the detail.
        JsonElement staffDetail = await GetJsonAsync(staffClient, $"/api/v1/variants/{variantId}");
        JsonElement staffList = await GetJsonAsync(staffClient, $"/api/v1/variants?productId={productId}");

        // Assert: no cost/margin/profit field appears anywhere in either Staff response, and the
        // role-safe sale prices + SKU are present (so this is an allowlist, not a blanket strip).
        AssertNoFinancialField(staffDetail);
        AssertNoFinancialField(staffList);

        Assert.Equal(sku, staffDetail.GetProperty("sku").GetString());
        Assert.Equal(49.99m, staffDetail.GetProperty("retailPrice").GetDecimal());
        Assert.Equal(29.99m, staffDetail.GetProperty("wholesalePrice").GetDecimal());

        JsonElement staffItems = staffList.GetProperty("items");
        Assert.Equal(1, staffItems.GetArrayLength());
        Assert.Equal(variantId, staffItems[0].GetProperty("variantId").GetGuid());

        // Positive control: the Owner reads the same variant successfully and receives the role-safe
        // fields. (Phase A's read model carries no cost/margin/profit figure yet; the Owner shape is
        // the reserved home for one — the point is that the Owner read path is allowed and intact
        // while the Staff path is structurally free of any such field.)
        JsonElement ownerDetail = await GetJsonAsync(ownerClient, $"/api/v1/variants/{variantId}");
        Assert.Equal(variantId, ownerDetail.GetProperty("variantId").GetGuid());
        Assert.Equal(sku, ownerDetail.GetProperty("sku").GetString());
        Assert.Equal(49.99m, ownerDetail.GetProperty("retailPrice").GetDecimal());
    }

    private static void AssertNoFinancialField(JsonElement element)
    {
        foreach (string propertyPath in EnumeratePropertyNames(element, parentPath: string.Empty))
        {
            foreach (string forbidden in s_forbiddenFinancialFragments)
            {
                Assert.False(
                    propertyPath.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"A Staff catalog response must not carry an Owner-only financial field, but '{propertyPath}' matched '{forbidden}'.");
            }
        }
    }

    // Walks every property name in the JSON tree (objects and nested arrays/objects) so the
    // structural-absence check covers nested shapes such as wood composition and dimensions.
    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element, string parentPath)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string path = parentPath.Length == 0 ? property.Name : $"{parentPath}.{property.Name}";
                    yield return path;
                    foreach (string nested in EnumeratePropertyNames(property.Value, path))
                    {
                        yield return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    foreach (string nested in EnumeratePropertyNames(item, parentPath))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static async Task<Guid> CreateProductAsync(HttpClient ownerClient)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new
            {
                name = "End-grain cutting board",
                publicDescription = "A durable end-grain board.",
            }),
        };
        AddMutationHeaders(request);

        using HttpResponseMessage response = await ownerClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        JsonElement body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid VariantId, string Sku)> CreateVariantAsync(HttpClient ownerClient, Guid productId)
    {
        string sku = $"SKU-ACC10-{Guid.NewGuid():N}";
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/variants")
        {
            Content = JsonContent.Create(new
            {
                productId,
                sku,
                trackingMode = "Quantity",
                unitOfMeasure = "each",
                retailPrice = 49.99m,
                wholesalePrice = 29.99m,
            }),
        };
        AddMutationHeaders(request);

        using HttpResponseMessage response = await ownerClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        JsonElement body = await ReadJsonAsync(response);
        return (body.GetProperty("id").GetGuid(), sku);
    }

    private static void AddMutationHeaders(HttpRequestMessage request) =>
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string requestUri)
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri);
        string diagBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {requestUri} -> {(int)response.StatusCode}: {diagBody}");
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
