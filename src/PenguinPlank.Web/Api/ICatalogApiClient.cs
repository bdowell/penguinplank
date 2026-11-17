using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Contracts.Common;

namespace PenguinPlank.Web.Api;

/// <summary>
/// Typed client for the <c>/api/v1</c> catalog endpoints: products, variants, pieces, and care
/// profile versions.
/// </summary>
/// <remarks>
/// Every mutating method takes an optional <c>idempotencyKey</c>. Pass the same key when
/// resubmitting a request after a <see cref="ApiErrorKind.Connectivity"/> failure so a request that
/// did reach the server is not applied twice; omit it and a new key is generated. Edit methods take
/// the <c>ETag</c> from the record last read and surface a changed record as
/// <see cref="ApiErrorKind.StaleVersion"/>. No operation is queued for later replay. Wood species
/// have no standalone endpoint in Phase A and so have no method here.
/// </remarks>
public interface ICatalogApiClient
{
    /// <summary>Lists products.</summary>
    /// <param name="query">The filters and paging.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page of products.</returns>
    Task<ApiResult<PagedResponse<ProductResponse>>> ListProductsAsync(ProductListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets a product.</summary>
    /// <param name="productId">The product id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The product, whose <c>ETag</c> is the token for a later edit.</returns>
    Task<ApiResult<ProductResponse>> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Creates a product.</summary>
    /// <param name="request">The new product.</param>
    /// <param name="idempotencyKey">The optional idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The id of the created product.</returns>
    Task<ApiResult<CreatedResource>> CreateProductAsync(CreateProductContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>Edits a product.</summary>
    /// <param name="productId">The product id.</param>
    /// <param name="request">The new values.</param>
    /// <param name="eTag">The <c>ETag</c> of the version being edited.</param>
    /// <param name="idempotencyKey">The optional idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success, or <see cref="ApiErrorKind.StaleVersion"/> when the product changed.</returns>
    Task<ApiResult> UpdateProductAsync(Guid productId, UpdateProductContract request, string eTag, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>Lists variants.</summary>
    /// <param name="query">The filters and paging.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page of variants.</returns>
    Task<ApiResult<PagedResponse<VariantResponse>>> ListVariantsAsync(VariantListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets a variant.</summary>
    /// <param name="variantId">The variant id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The variant, whose <c>ETag</c> is the token for a later edit.</returns>
    Task<ApiResult<VariantResponse>> GetVariantAsync(Guid variantId, CancellationToken cancellationToken = default);

    /// <summary>Creates a variant.</summary>
    /// <param name="request">The new variant.</param>
    /// <param name="idempotencyKey">The optional idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The id of the created variant.</returns>
    Task<ApiResult<CreatedResource>> CreateVariantAsync(CreateVariantContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>Edits a variant.</summary>
    /// <param name="variantId">The variant id.</param>
    /// <param name="request">The new values.</param>
    /// <param name="eTag">The <c>ETag</c> of the version being edited.</param>
    /// <param name="idempotencyKey">The optional idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success, or <see cref="ApiErrorKind.StaleVersion"/> when the variant changed.</returns>
    Task<ApiResult> UpdateVariantAsync(Guid variantId, UpdateVariantContract request, string eTag, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>Lists pieces.</summary>
    /// <param name="query">The filters and paging.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page of pieces.</returns>
    Task<ApiResult<PagedResponse<PieceResponse>>> ListPiecesAsync(PieceListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets a piece.</summary>
    /// <param name="pieceId">The piece id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The piece.</returns>
    Task<ApiResult<PieceResponse>> GetPieceAsync(Guid pieceId, CancellationToken cancellationToken = default);

    /// <summary>Creates a piece.</summary>
    /// <param name="request">The new piece.</param>
    /// <param name="idempotencyKey">The optional idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The id of the created piece.</returns>
    Task<ApiResult<CreatedResource>> CreatePieceAsync(CreatePieceContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the versions of a care profile.</summary>
    /// <param name="careProfileId">The care profile id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The versions.</returns>
    Task<ApiResult<IReadOnlyList<CareProfileVersionResponse>>> GetCareProfileVersionsAsync(Guid careProfileId, CancellationToken cancellationToken = default);

    /// <summary>Adds a version to a care profile.</summary>
    /// <param name="careProfileId">The care profile id.</param>
    /// <param name="request">The new guidance.</param>
    /// <param name="idempotencyKey">The optional idempotency key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created version.</returns>
    Task<ApiResult<CareProfileVersionResponse>> AddCareProfileVersionAsync(Guid careProfileId, AddCareVersionContract request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}
