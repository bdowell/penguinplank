using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Contracts.Common;

namespace PenguinPlank.Web.Api;

/// <summary>Default <see cref="ICatalogApiClient"/> over <see cref="ApiRequestSender"/>.</summary>
public sealed class CatalogApiClient : ICatalogApiClient
{
    private const string Products = "api/v1/products";
    private const string Variants = "api/v1/variants";
    private const string Pieces = "api/v1/pieces";
    private const string CareProfiles = "api/v1/care-profiles";

    private readonly ApiRequestSender _sender;

    /// <summary>Initializes the client.</summary>
    /// <param name="sender">The request sender.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sender"/> is <see langword="null"/>.</exception>
    public CatalogApiClient(ApiRequestSender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResponse<ProductResponse>>> ListProductsAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _sender.GetAsync<PagedResponse<ProductResponse>>($"{Products}{query.ToQueryString()}", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<ProductResponse>> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _sender.GetAsync<ProductResponse>($"{Products}/{productId:D}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<CreatedResource>> CreateProductAsync(CreateProductContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sender.SendAsync<CreateProductContract, CreatedResource>(HttpMethod.Post, Products, request, ifMatch: null, idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult> UpdateProductAsync(Guid productId, UpdateProductContract request, string eTag, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(eTag);
        return _sender.SendAsync(HttpMethod.Patch, $"{Products}/{productId:D}", request, eTag, idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResponse<VariantResponse>>> ListVariantsAsync(VariantListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _sender.GetAsync<PagedResponse<VariantResponse>>($"{Variants}{query.ToQueryString()}", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<VariantResponse>> GetVariantAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        _sender.GetAsync<VariantResponse>($"{Variants}/{variantId:D}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<CreatedResource>> CreateVariantAsync(CreateVariantContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sender.SendAsync<CreateVariantContract, CreatedResource>(HttpMethod.Post, Variants, request, ifMatch: null, idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult> UpdateVariantAsync(Guid variantId, UpdateVariantContract request, string eTag, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(eTag);
        return _sender.SendAsync(HttpMethod.Patch, $"{Variants}/{variantId:D}", request, eTag, idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<PagedResponse<PieceResponse>>> ListPiecesAsync(PieceListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _sender.GetAsync<PagedResponse<PieceResponse>>($"{Pieces}{query.ToQueryString()}", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<PieceResponse>> GetPieceAsync(Guid pieceId, CancellationToken cancellationToken = default) =>
        _sender.GetAsync<PieceResponse>($"{Pieces}/{pieceId:D}", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<CreatedResource>> CreatePieceAsync(CreatePieceContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sender.SendAsync<CreatePieceContract, CreatedResource>(HttpMethod.Post, Pieces, request, ifMatch: null, idempotencyKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<CareProfileVersionResponse>>> GetCareProfileVersionsAsync(Guid careProfileId, CancellationToken cancellationToken = default) =>
        _sender.GetAsync<IReadOnlyList<CareProfileVersionResponse>>($"{CareProfiles}/{careProfileId:D}/versions", cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<CareProfileVersionResponse>> AddCareProfileVersionAsync(Guid careProfileId, AddCareVersionContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sender.SendAsync<AddCareVersionContract, CareProfileVersionResponse>(
            HttpMethod.Post, $"{CareProfiles}/{careProfileId:D}/versions", request, ifMatch: null, idempotencyKey, cancellationToken);
    }
}
