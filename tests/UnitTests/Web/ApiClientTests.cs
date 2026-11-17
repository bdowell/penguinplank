using System.Net;
using PenguinPlank.Contracts.Authentication;
using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Web.Api;

namespace PenguinPlank.UnitTests.Web;

public sealed class ApiClientTests
{
    private const string CsrfJson = """{"requestToken":"csrf-1","headerName":"X-CSRF-TOKEN"}""";
    private static readonly Uri s_origin = new("https://app.example/");

    private sealed class SequentialKeys : IIdempotencyKeyGenerator
    {
        private int _next;

        public string NewKey() => $"key-{++_next}";
    }

    private static (CatalogApiClient Catalog, AuthApiClient Auth, FakeHttpMessageHandler Handler) Create(
        Func<RecordedRequest, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = s_origin };
        var sender = new ApiRequestSender(http, new SequentialKeys());
        return (new CatalogApiClient(sender), new AuthApiClient(sender), handler);
    }

    private static HttpResponseMessage Route(RecordedRequest request, HttpResponseMessage mutationResponse) =>
        request.Uri.EndsWith(ApiRequestSender.CsrfPath, StringComparison.Ordinal)
            ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, CsrfJson)
            : mutationResponse;

    private static readonly CreateProductContract s_newProduct = new() { Name = "Plank" };

    [Fact]
    public async Task CreateProductAsync_Mutation_SendsCsrfAndIdempotencyHeadersWithoutBearerTokenAsync()
    {
        var (catalog, _, handler) = Create(r => Route(r, FakeHttpMessageHandler.Json(HttpStatusCode.Created, """{"id":"11111111-1111-1111-1111-111111111111"}""")));

        ApiResult<CreatedResource> result = await catalog.CreateProductAsync(s_newProduct);

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.Value!.Id);
        RecordedRequest post = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Equal("https://app.example/api/v1/products", post.Uri);
        Assert.Equal("csrf-1", post.Headers["X-CSRF-TOKEN"]);
        Assert.Equal("key-1", post.Headers["Idempotency-Key"]);
        Assert.False(post.Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task CreateProductAsync_CallerSuppliedKey_UsesThatKeyAsync()
    {
        var (catalog, _, handler) = Create(r => Route(r, FakeHttpMessageHandler.Json(HttpStatusCode.Created, """{"id":"11111111-1111-1111-1111-111111111111"}""")));

        await catalog.CreateProductAsync(s_newProduct, idempotencyKey: "retry-key");

        Assert.Equal("retry-key", handler.Requests.Single(r => r.Method == HttpMethod.Post).Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task CreateProductAsync_TwoMutations_FetchesCsrfTokenOnceAsync()
    {
        var (catalog, _, handler) = Create(r => Route(r, FakeHttpMessageHandler.Json(HttpStatusCode.Created, """{"id":"11111111-1111-1111-1111-111111111111"}""")));

        await catalog.CreateProductAsync(s_newProduct);
        await catalog.CreateProductAsync(s_newProduct);

        Assert.Single(handler.Requests, r => r.Uri.EndsWith(ApiRequestSender.CsrfPath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetProductAsync_Read_SendsNoMutationHeadersAndNoCsrfFetchAsync()
    {
        const string json = """{"productId":"11111111-1111-1111-1111-111111111111","name":"Plank","publicationState":"Draft","isActive":true,"eTag":"v7","createdAt":"2025-01-01T00:00:00+00:00","updatedAt":"2025-01-01T00:00:00+00:00"}""";
        var (catalog, _, handler) = Create(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, json));

        ApiResult<ProductResponse> result = await catalog.GetProductAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal("v7", result.Value!.ETag);
        RecordedRequest only = Assert.Single(handler.Requests);
        Assert.False(only.Headers.ContainsKey("Idempotency-Key"));
        Assert.False(only.Headers.ContainsKey("X-CSRF-TOKEN"));
    }

    [Fact]
    public async Task UpdateProductAsync_Edit_SendsQuotedIfMatchAndMapsNoContentToSuccessAsync()
    {
        var (catalog, _, handler) = Create(r => Route(r, new HttpResponseMessage(HttpStatusCode.NoContent)));

        ApiResult result = await catalog.UpdateProductAsync(Guid.NewGuid(), new UpdateProductContract { Name = "Plank" }, "v7");

        Assert.True(result.IsSuccess);
        RecordedRequest patch = handler.Requests.Single(r => r.Method == HttpMethod.Patch);
        Assert.Equal("\"v7\"", patch.Headers["If-Match"]);
    }

    [Fact]
    public async Task UpdateProductAsync_PreconditionFailed_ReturnsStaleVersionErrorAsync()
    {
        const string problem = """{"status":412,"title":"Precondition failed","detail":"The record changed.","errorCode":"StaleVersion","correlationId":"abc"}""";
        var (catalog, _, _) = Create(r => Route(r, FakeHttpMessageHandler.Json(HttpStatusCode.PreconditionFailed, problem)));

        ApiResult result = await catalog.UpdateProductAsync(Guid.NewGuid(), new UpdateProductContract { Name = "Plank" }, "v7");

        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsStaleVersion);
        Assert.Equal("StaleVersion", result.Error.ErrorCode);
        Assert.Equal("abc", result.Error.CorrelationId);
    }

    [Fact]
    public async Task CreateProductAsync_ConnectionLost_ReturnsConnectivityErrorAndSendsOnceAsync()
    {
        var (catalog, _, handler) = Create(r => r.Uri.EndsWith(ApiRequestSender.CsrfPath, StringComparison.Ordinal)
            ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, CsrfJson)
            : throw new HttpRequestException("offline"));

        ApiResult<CreatedResource> result = await catalog.CreateProductAsync(s_newProduct);

        Assert.True(result.Error!.IsConnectivityLoss);
        Assert.Equal(1, handler.Requests.Count(r => r.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task CreateProductAsync_CsrfFetchFailsOffline_ReturnsConnectivityErrorWithoutPostingAsync()
    {
        var (catalog, _, handler) = Create(_ => throw new HttpRequestException("offline"));

        ApiResult<CreatedResource> result = await catalog.CreateProductAsync(s_newProduct);

        Assert.True(result.Error!.IsConnectivityLoss);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task CreateProductAsync_CallerCancels_PropagatesAsync()
    {
        var (catalog, _, _) = Create(r => Route(r, new HttpResponseMessage(HttpStatusCode.Created)));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalog.CreateProductAsync(s_newProduct, cancellationToken: cts.Token));
    }

    [Theory]
    [InlineData(400, ApiErrorKind.Validation)]
    [InlineData(401, ApiErrorKind.Unauthenticated)]
    [InlineData(403, ApiErrorKind.Forbidden)]
    [InlineData(404, ApiErrorKind.NotFound)]
    [InlineData(409, ApiErrorKind.Conflict)]
    [InlineData(412, ApiErrorKind.StaleVersion)]
    [InlineData(413, ApiErrorKind.PayloadTooLarge)]
    [InlineData(429, ApiErrorKind.RateLimited)]
    [InlineData(500, ApiErrorKind.Server)]
    public void FromResponse_Status_MapsKind(int status, ApiErrorKind expected)
    {
        ApiError error = ApiErrorMapper.FromResponse(status, body: null);

        Assert.Equal(expected, error.Kind);
        Assert.Equal(status, error.StatusCode);
    }

    [Fact]
    public void FromResponse_ProblemDetailsBody_CarriesDetailCodeAndCorrelationId()
    {
        ApiError error = ApiErrorMapper.FromResponse(
            409,
            """{"title":"Conflict","detail":"SKU exists.","errorCode":"DuplicateSku","correlationId":"c-9"}""");

        Assert.Equal(ApiErrorKind.Conflict, error.Kind);
        Assert.Equal("SKU exists.", error.Detail);
        Assert.Equal("DuplicateSku", error.ErrorCode);
        Assert.Equal("c-9", error.CorrelationId);
    }

    [Fact]
    public void FromResponse_NonJsonBody_FallsBackToStatus()
    {
        ApiError error = ApiErrorMapper.FromResponse(502, "<html>bad gateway</html>");

        Assert.Equal(ApiErrorKind.Server, error.Kind);
        Assert.Null(error.Detail);
    }

    [Fact]
    public async Task LoginAsync_Success_SendsCredentialsAndRefetchesCsrfOnNextMutationAsync()
    {
        const string actor = """{"userId":"22222222-2222-2222-2222-222222222222","email":"a@b.c","role":"Owner"}""";
        var (catalog, auth, handler) = Create(r =>
            r.Uri.EndsWith("auth/login", StringComparison.Ordinal)
                ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, actor)
                : Route(r, FakeHttpMessageHandler.Json(HttpStatusCode.Created, """{"id":"11111111-1111-1111-1111-111111111111"}""")));

        ApiResult<CurrentActorResponse> login = await auth.LoginAsync(new LoginContract { Email = "a@b.c", Password = "pw" });
        await catalog.CreateProductAsync(s_newProduct);

        Assert.Equal("Owner", login.Value!.Role);
        Assert.Equal(2, handler.Requests.Count(r => r.Uri.EndsWith(ApiRequestSender.CsrfPath, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task LoginAsync_BadCredentials_ReturnsUnauthenticatedAsync()
    {
        var (_, auth, _) = Create(r => Route(r, FakeHttpMessageHandler.Json(HttpStatusCode.Unauthorized, """{"status":401,"detail":"Invalid credentials."}""")));

        ApiResult<CurrentActorResponse> login = await auth.LoginAsync(new LoginContract { Email = "a@b.c", Password = "bad" });

        Assert.Equal(ApiErrorKind.Unauthenticated, login.Error!.Kind);
    }

    [Fact]
    public async Task ListProductsAsync_Filters_BuildsEscapedQueryStringAsync()
    {
        var (catalog, _, handler) = Create(_ => FakeHttpMessageHandler.Json(
            HttpStatusCode.OK, """{"items":[],"totalCount":0,"pageNumber":2,"pageSize":10}"""));

        ApiResult<PenguinPlank.Contracts.Common.PagedResponse<ProductResponse>> result = await catalog.ListProductsAsync(new ProductListQuery
        {
            Search = "oak & ash",
            IncludeArchived = true,
            Paging = new PagingOptions(Page: 2, PageSize: 10, Sort: "name", Descending: true),
        });

        Assert.Equal(2, result.Value!.PageNumber);
        Assert.Equal(
            "https://app.example/api/v1/products?search=oak%20%26%20ash&includeArchived=true&page=2&pageSize=10&sort=name&desc=true",
            Assert.Single(handler.Requests).Uri);
    }

    [Fact]
    public void ToQueryString_NoFilters_IsEmpty()
    {
        Assert.Equal(string.Empty, new VariantListQuery().ToQueryString());
        Assert.Equal(string.Empty, new PieceListQuery().ToQueryString());
    }

    [Fact]
    public async Task GetAsync_MalformedSuccessBody_ReturnsUnexpectedErrorAsync()
    {
        var (catalog, _, _) = Create(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, "not json"));

        ApiResult<ProductResponse> result = await catalog.GetProductAsync(Guid.NewGuid());

        Assert.Equal(ApiErrorKind.Unexpected, result.Error!.Kind);
    }
}
