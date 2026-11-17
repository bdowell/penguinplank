using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Contracts.Common;
using PenguinPlank.Web.Api;
using PenguinPlank.Web.Pages.Catalog;

namespace PenguinPlank.UnitTests.Web;

/// <summary>
/// Component tests for the product and variant edit pages: a successful save, the stale-version
/// conflict and refresh flow (acceptance scenario 13), connectivity loss, and accessible labels.
/// </summary>
public sealed class CatalogEditComponentTests : IDisposable
{
    private static readonly Guid s_productId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid s_variantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly BunitContext _context = new();
    private readonly FakeCatalogApiClient _api = new();

    public CatalogEditComponentTests()
    {
        _context.Services.AddSingleton<ICatalogApiClient>(_api);
        _context.Services.AddSingleton<IIdempotencyKeyGenerator>(new SequentialKeys());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ProductEdit_EditAndSave_SendsTrimmedChangeWithLoadedVersionAndShowsSavedAsync()
    {
        _api.ProductReads.Enqueue(Product("Plank", "v1"));
        _api.ProductReads.Enqueue(Product("Board", "v2"));
        _api.ProductUpdates.Enqueue(ApiResult.Success());
        IRenderedComponent<ProductEdit> page = RenderProductEdit();

        page.Find("#product-name").Input("  Board ");
        page.Find("form").Submit();
        await WaitForTextAsync(page, "Saved.");

        ProductUpdateCall call = Assert.Single(_api.ProductUpdateCalls);
        Assert.Equal("Board", call.Request.Name);
        Assert.Equal("v1", call.ETag);
        Assert.Equal("key-1", call.Key);
        Assert.Equal("Board", page.Find("#product-name").GetAttribute("value"));
        Assert.Equal("status", page.Find("div.cp-status").GetAttribute("role"));
    }

    [Fact]
    public async Task ProductEdit_BlankName_ShowsProblemAndDoesNotSaveAsync()
    {
        _api.ProductReads.Enqueue(Product("Plank", "v1"));
        IRenderedComponent<ProductEdit> page = RenderProductEdit();

        page.Find("#product-name").Input(" ");
        page.Find("form").Submit();
        await WaitForTextAsync(page, "Name is required.");

        Assert.Empty(_api.ProductUpdateCalls);
    }

    [Fact]
    public async Task ProductEdit_StaleVersion_ShowsConflictKeepsEditAndOffersRefreshAsync()
    {
        _api.ProductReads.Enqueue(Product("Plank", "v1"));
        _api.ProductUpdates.Enqueue(Fail(ApiErrorKind.StaleVersion));
        IRenderedComponent<ProductEdit> page = RenderProductEdit();

        page.Find("#product-name").Input("My edit");
        page.Find("form").Submit();
        await WaitForTextAsync(page, "changed by someone else");

        Assert.Equal("alert", page.Find("div.cp-status").GetAttribute("role"));
        Assert.Equal("assertive", page.Find("div.cp-status").GetAttribute("aria-live"));
        Assert.NotNull(FindButton(page, "Reload latest version"));
        Assert.True(FindButton(page, "Save changes").HasAttribute("disabled"));
        Assert.Equal("My edit", page.Find("#product-name").GetAttribute("value"));
        Assert.Single(_api.ProductUpdateCalls);
        Assert.Equal(1, _api.ProductReadCount);
    }

    [Fact]
    public async Task ProductEdit_StaleVersionThenRefresh_ReloadsLatestAndAllowsSavingAgainAsync()
    {
        _api.ProductReads.Enqueue(Product("Plank", "v1"));
        _api.ProductReads.Enqueue(Product("Server name", "v2"));
        _api.ProductReads.Enqueue(Product("Final", "v3"));
        _api.ProductUpdates.Enqueue(Fail(ApiErrorKind.StaleVersion));
        _api.ProductUpdates.Enqueue(ApiResult.Success());
        IRenderedComponent<ProductEdit> page = RenderProductEdit();
        page.Find("#product-name").Input("My edit");
        page.Find("form").Submit();
        await WaitForTextAsync(page, "changed by someone else");

        FindButton(page, "Reload latest version").Click();
        await WaitForValueAsync(page, "#product-name", "Server name");

        Assert.Equal("Server name", page.Find("#product-name").GetAttribute("value"));
        Assert.DoesNotContain("changed by someone else", page.Markup, StringComparison.Ordinal);
        Assert.Throws<ElementNotFoundException>(() => FindButton(page, "Reload latest version"));
        Assert.False(FindButton(page, "Save changes").HasAttribute("disabled"));

        page.Find("#product-name").Input("Final");
        page.Find("form").Submit();
        await WaitForTextAsync(page, "Saved.");

        Assert.Equal(["v1", "v2"], _api.ProductUpdateCalls.Select(c => c.ETag));
    }

    [Fact]
    public async Task ProductEdit_ConnectivityLoss_ShowsUnsavedStateAndRetryReusesKeyAsync()
    {
        _api.ProductReads.Enqueue(Product("Plank", "v1"));
        _api.ProductReads.Enqueue(Product("Board", "v2"));
        _api.ProductUpdates.Enqueue(Fail(ApiErrorKind.Connectivity));
        _api.ProductUpdates.Enqueue(ApiResult.Success());
        IRenderedComponent<ProductEdit> page = RenderProductEdit();
        page.Find("#product-name").Input("Board");

        page.Find("form").Submit();
        await WaitForTextAsync(page, "Your change is not saved and nothing is queued");

        Assert.Equal("alert", page.Find("div.cp-status").GetAttribute("role"));
        Assert.Equal("Board", page.Find("#product-name").GetAttribute("value"));
        Assert.False(FindButton(page, "Try again").HasAttribute("disabled"));
        Assert.Throws<ElementNotFoundException>(() => FindButton(page, "Reload latest version"));

        page.Find("form").Submit();
        await WaitForTextAsync(page, "Saved.");

        Assert.Equal(["key-1", "key-1"], _api.ProductUpdateCalls.Select(c => c.Key));
    }

    [Fact]
    public async Task ProductEdit_LoadFailure_ShowsErrorAndTryAgainReloadsAsync()
    {
        _api.ProductReads.Enqueue(ApiResult.Failure<ProductResponse>(Error(ApiErrorKind.Connectivity)));
        _api.ProductReads.Enqueue(Product("Plank", "v1"));
        IRenderedComponent<ProductEdit> page = RenderProductEdit();

        await WaitForTextAsync(page, "Could not reach the server");
        FindButton(page, "Try again").Click();
        await WaitForTextAsync(page, "Save changes");

        Assert.Equal("Plank", page.Find("#product-name").GetAttribute("value"));
    }

    [Theory]
    [InlineData("product-name")]
    [InlineData("product-category")]
    [InlineData("product-description")]
    [InlineData("product-notes")]
    public void ProductEdit_Fields_HaveAssociatedLabel(string fieldId)
    {
        _api.ProductReads.Enqueue(Product("Plank", "v1"));

        IRenderedComponent<ProductEdit> page = RenderProductEdit();

        Assert.False(string.IsNullOrWhiteSpace(page.Find($"label[for='{fieldId}']").TextContent));
        Assert.NotNull(page.Find($"#{fieldId}"));
        Assert.Equal("true", page.Find("#product-name").GetAttribute("aria-required"));
        Assert.Equal("Product details", page.Find("fieldset > legend").TextContent);
    }

    [Fact]
    public async Task VariantEdit_EditAndSave_SendsChangeWithLoadedVersionAndShowsSavedAsync()
    {
        _api.VariantReads.Enqueue(Variant("Matte", "v1"));
        _api.VariantReads.Enqueue(Variant("Gloss", "v2"));
        _api.VariantUpdates.Enqueue(ApiResult.Success());
        IRenderedComponent<VariantEdit> page = RenderVariantEdit();

        page.Find("#variant-finish").Input("Gloss");
        page.Find("form").Submit();
        await WaitForTextAsync(page, "Saved.");

        VariantUpdateCall call = Assert.Single(_api.VariantUpdateCalls);
        Assert.Equal("Gloss", call.Request.Finish);
        Assert.Equal("each", call.Request.UnitOfMeasure);
        Assert.Equal("v1", call.ETag);
        Assert.Equal("Gloss", page.Find("#variant-finish").GetAttribute("value"));
    }

    [Fact]
    public async Task VariantEdit_StaleVersionThenRefresh_ShowsConflictThenReloadsLatestAsync()
    {
        _api.VariantReads.Enqueue(Variant("Matte", "v1"));
        _api.VariantReads.Enqueue(Variant("Satin", "v2"));
        _api.VariantUpdates.Enqueue(Fail(ApiErrorKind.StaleVersion));
        IRenderedComponent<VariantEdit> page = RenderVariantEdit();
        page.Find("#variant-finish").Input("Gloss");

        page.Find("form").Submit();
        await WaitForTextAsync(page, "changed by someone else");

        Assert.True(FindButton(page, "Save changes").HasAttribute("disabled"));
        Assert.Equal("Gloss", page.Find("#variant-finish").GetAttribute("value"));
        Assert.Equal(1, _api.VariantReadCount);

        FindButton(page, "Reload latest version").Click();
        await WaitForValueAsync(page, "#variant-finish", "Satin");

        Assert.Equal("Satin", page.Find("#variant-finish").GetAttribute("value"));
        Assert.False(FindButton(page, "Save changes").HasAttribute("disabled"));
    }

    [Fact]
    public async Task VariantEdit_ConnectivityLoss_ShowsUnsavedStateAsync()
    {
        _api.VariantReads.Enqueue(Variant("Matte", "v1"));
        _api.VariantUpdates.Enqueue(Fail(ApiErrorKind.Connectivity));
        IRenderedComponent<VariantEdit> page = RenderVariantEdit();
        page.Find("#variant-finish").Input("Gloss");

        page.Find("form").Submit();
        await WaitForTextAsync(page, "Your change is not saved and nothing is queued");

        Assert.Equal("alert", page.Find("div.cp-status").GetAttribute("role"));
        Assert.Equal("Gloss", page.Find("#variant-finish").GetAttribute("value"));
        Assert.NotNull(FindButton(page, "Try again"));
    }

    [Theory]
    [InlineData("variant-unit")]
    [InlineData("variant-barcode")]
    [InlineData("variant-finish")]
    [InlineData("variant-case-pack")]
    [InlineData("variant-retail")]
    [InlineData("variant-wholesale")]
    [InlineData("variant-length")]
    [InlineData("variant-width")]
    [InlineData("variant-thickness")]
    [InlineData("variant-diameter")]
    [InlineData("variant-dimension-unit")]
    public void VariantEdit_Fields_HaveAssociatedLabel(string fieldId)
    {
        _api.VariantReads.Enqueue(Variant("Matte", "v1"));

        IRenderedComponent<VariantEdit> page = RenderVariantEdit();

        Assert.False(string.IsNullOrWhiteSpace(page.Find($"label[for='{fieldId}']").TextContent));
        Assert.NotNull(page.Find($"#{fieldId}"));
        Assert.Equal("true", page.Find("#variant-unit").GetAttribute("aria-required"));
    }

    private static async Task WaitForTextAsync<TComponent>(IRenderedComponent<TComponent> page, string text)
        where TComponent : IComponent
    {
        await page.WaitForAssertionAsync(
            () => Assert.Contains(text, page.Markup, StringComparison.Ordinal),
            TimeSpan.FromSeconds(5));
    }

    private static async Task WaitForValueAsync<TComponent>(IRenderedComponent<TComponent> page, string selector, string expected)
        where TComponent : IComponent
    {
        await page.WaitForAssertionAsync(
            () => Assert.Equal(expected, page.Find(selector).GetAttribute("value")),
            TimeSpan.FromSeconds(5));
    }

    private static IElement FindButton<TComponent>(IRenderedComponent<TComponent> page, string label)
        where TComponent : IComponent
    {
        return page.FindAll("button").FirstOrDefault(button => button.TextContent.Trim() == label)
            ?? throw new ElementNotFoundException($"button '{label}'");
    }

    private static ApiError Error(ApiErrorKind kind) => new(kind, null, null, "title", null, null);

    private static ApiResult Fail(ApiErrorKind kind) => ApiResult.Failure(Error(kind));

    private static ApiResult<ProductResponse> Product(string name, string eTag) =>
        ApiResult.Success(new ProductResponse
        {
            ProductId = s_productId,
            Name = name,
            PublicationState = "Draft",
            IsActive = true,
            ETag = eTag,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        });

    private static ApiResult<VariantResponse> Variant(string finish, string eTag) =>
        ApiResult.Success(new VariantResponse
        {
            VariantId = s_variantId,
            ProductId = s_productId,
            Sku = "PLANK-1",
            TrackingMode = "Quantity",
            UnitOfMeasure = "each",
            Finish = finish,
            Currency = "USD",
            IsActive = true,
            ETag = eTag,
        });

    private IRenderedComponent<ProductEdit> RenderProductEdit() =>
        _context.Render<ProductEdit>(parameters => parameters.Add(page => page.ProductId, s_productId));

    private IRenderedComponent<VariantEdit> RenderVariantEdit() =>
        _context.Render<VariantEdit>(parameters => parameters.Add(page => page.VariantId, s_variantId));

    private sealed class SequentialKeys : IIdempotencyKeyGenerator
    {
        private int _next;

        public string NewKey() => $"key-{++_next}";
    }

    private sealed record ProductUpdateCall(UpdateProductContract Request, string ETag, string? Key);

    private sealed record VariantUpdateCall(UpdateVariantContract Request, string ETag, string? Key);

    /// <summary>A scripted catalog client. Only the product and variant read and edit calls are supported.</summary>
    private sealed class FakeCatalogApiClient : ICatalogApiClient
    {
        public Queue<ApiResult<ProductResponse>> ProductReads { get; } = new();

        public Queue<ApiResult> ProductUpdates { get; } = new();

        public Queue<ApiResult<VariantResponse>> VariantReads { get; } = new();

        public Queue<ApiResult> VariantUpdates { get; } = new();

        public List<ProductUpdateCall> ProductUpdateCalls { get; } = [];

        public List<VariantUpdateCall> VariantUpdateCalls { get; } = [];

        public int ProductReadCount { get; private set; }

        public int VariantReadCount { get; private set; }

        public Task<ApiResult<ProductResponse>> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            ProductReadCount++;
            return Task.FromResult(ProductReads.Dequeue());
        }

        public Task<ApiResult> UpdateProductAsync(Guid productId, UpdateProductContract request, string eTag, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            ProductUpdateCalls.Add(new ProductUpdateCall(request, eTag, idempotencyKey));
            return Task.FromResult(ProductUpdates.Dequeue());
        }

        public Task<ApiResult<VariantResponse>> GetVariantAsync(Guid variantId, CancellationToken cancellationToken = default)
        {
            VariantReadCount++;
            return Task.FromResult(VariantReads.Dequeue());
        }

        public Task<ApiResult> UpdateVariantAsync(Guid variantId, UpdateVariantContract request, string eTag, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            VariantUpdateCalls.Add(new VariantUpdateCall(request, eTag, idempotencyKey));
            return Task.FromResult(VariantUpdates.Dequeue());
        }

        public Task<ApiResult<PagedResponse<ProductResponse>>> ListProductsAsync(ProductListQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<CreatedResource>> CreateProductAsync(CreateProductContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<PagedResponse<VariantResponse>>> ListVariantsAsync(VariantListQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<CreatedResource>> CreateVariantAsync(CreateVariantContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<PagedResponse<PieceResponse>>> ListPiecesAsync(PieceListQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<PieceResponse>> GetPieceAsync(Guid pieceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<CreatedResource>> CreatePieceAsync(CreatePieceContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<IReadOnlyList<CareProfileVersionResponse>>> GetCareProfileVersionsAsync(Guid careProfileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<CareProfileVersionResponse>> AddCareProfileVersionAsync(Guid careProfileId, AddCareVersionContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
