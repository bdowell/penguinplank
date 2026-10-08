using PenguinPlank.Application.Common;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The persistence boundary for catalog <b>queries</b>: listing and fetching products, variants,
/// and pieces as read models (requirements R01 §1.1, §1.6; A4 §6.1).
/// </summary>
/// <remarks>
/// <para>
/// This is a narrow, responsibility-named boundary interface defined in Application and
/// implemented in Infrastructure over EF Core (coding-standards §2, §3). The implementation runs
/// parameterized queries and materializes flat read models; it never returns an <c>IQueryable</c>,
/// a <c>DbContext</c>, or an EF entity across the boundary, and it does not leak EF tracking
/// behavior. List operations accept a query/filter paired with a <see cref="PageRequest"/> and
/// return a <see cref="Page{T}"/> with a total count under the Phase A paging policy
/// (requirement A4 §6.1).
/// </para>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/>. A get-by-id returns the
/// read model or <see langword="null"/> when no record matches; the reader does not throw for an
/// absent record (coding-standards §6). These read models are the full authenticated shapes; the
/// API maps them into the role-scoped public/Owner projection shapes before responding
/// (requirement A2 §5.11).
/// </para>
/// </remarks>
public interface ICatalogReader
{
    /// <summary>Lists products matching the query, as a page with a total count.</summary>
    /// <param name="query">The product filter and paging intent.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A page of <see cref="ProductView"/> read models.</returns>
    Task<Page<ProductView>> ListProductsAsync(ProductQuery query, CancellationToken cancellationToken);

    /// <summary>Fetches a single product by identifier.</summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ProductView"/>, or <see langword="null"/> when no product matches.</returns>
    Task<ProductView?> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Lists variants matching the query, as a page with a total count.</summary>
    /// <param name="query">The variant filter and paging intent.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A page of <see cref="ProductVariantView"/> read models.</returns>
    Task<Page<ProductVariantView>> ListVariantsAsync(CatalogQuery query, CancellationToken cancellationToken);

    /// <summary>Fetches a single variant by identifier.</summary>
    /// <param name="variantId">The variant identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ProductVariantView"/>, or <see langword="null"/> when no variant matches.</returns>
    Task<ProductVariantView?> GetVariantAsync(Guid variantId, CancellationToken cancellationToken);

    /// <summary>Lists pieces matching the query, as a page with a total count.</summary>
    /// <param name="query">The piece filter and paging intent.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A page of <see cref="ProductPieceView"/> read models.</returns>
    Task<Page<ProductPieceView>> ListPiecesAsync(PieceQuery query, CancellationToken cancellationToken);

    /// <summary>Fetches a single piece by identifier.</summary>
    /// <param name="pieceId">The piece identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ProductPieceView"/>, or <see langword="null"/> when no piece matches.</returns>
    Task<ProductPieceView?> GetPieceAsync(Guid pieceId, CancellationToken cancellationToken);
}
