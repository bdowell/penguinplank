using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Common;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Builds the Application catalog query records (<see cref="ProductQuery"/>,
/// <see cref="CatalogQuery"/>, <see cref="PieceQuery"/>) from the raw <c>GET</c>-list query-string
/// parameters, applying the shared paging/sort intent (requirement A4 §6.1).
/// </summary>
/// <remarks>
/// <para>
/// A pure, directly testable translation (coding-standards §1): it turns loose, optional
/// query-string values into the typed query records the reader accepts, and composes the
/// <see cref="PageRequest"/> that the reader then normalizes (default size 50, max 200, stable
/// sort). An unrecognized publication-state filter is a malformed request, surfaced as a
/// <see cref="CatalogContractFormatException"/> the endpoint maps to a <c>400</c> validation
/// response rather than silently ignoring the filter.
/// </para>
/// </remarks>
public static class CatalogQueryBinder
{
    /// <summary>Builds a page request from the shared paging/sort query-string parameters.</summary>
    /// <param name="page">The requested one-based page number, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">The requested page size, or <see langword="null"/> for the default.</param>
    /// <param name="sort">The optional sort field name.</param>
    /// <param name="desc">Whether to sort descending; defaults to ascending.</param>
    /// <returns>A page request carrying the caller's paging intent (clamped later by the reader).</returns>
    public static PageRequest BuildPageRequest(int? page, int? pageSize, string? sort, bool desc)
    {
        return new PageRequest(
            pageNumber: page ?? PageRequest.MinimumPageNumber,
            pageSize: pageSize,
            sortField: sort,
            sortDirection: desc ? SortDirection.Descending : SortDirection.Ascending);
    }

    /// <summary>Builds the product list query.</summary>
    /// <param name="search">An optional free-text term matched against name and category.</param>
    /// <param name="category">An optional exact category filter.</param>
    /// <param name="publicationState">An optional publication-state filter string.</param>
    /// <param name="includeArchived">Whether to include archived products.</param>
    /// <param name="pageRequest">The paging/sort intent.</param>
    /// <returns>The typed product query.</returns>
    /// <exception cref="CatalogContractFormatException">Thrown when <paramref name="publicationState"/> is not a recognized state.</exception>
    public static ProductQuery BuildProductQuery(
        string? search,
        string? category,
        string? publicationState,
        bool includeArchived,
        PageRequest pageRequest)
    {
        return new ProductQuery
        {
            SearchTerm = NullIfBlank(search),
            Category = NullIfBlank(category),
            PublicationState = ParsePublicationState(publicationState),
            IncludeArchived = includeArchived,
            Page = pageRequest,
        };
    }

    /// <summary>Builds the variant list query.</summary>
    /// <param name="productId">An optional owning-product filter.</param>
    /// <param name="search">An optional free-text term matched against SKU and product name.</param>
    /// <param name="trackingMode">An optional tracking-mode filter string.</param>
    /// <param name="publicationState">An optional publication-state filter string.</param>
    /// <param name="includeArchived">Whether to include archived variants.</param>
    /// <param name="pageRequest">The paging/sort intent.</param>
    /// <returns>The typed variant query.</returns>
    /// <exception cref="CatalogContractFormatException">Thrown when a filter string is not recognized.</exception>
    public static CatalogQuery BuildVariantQuery(
        Guid? productId,
        string? search,
        string? trackingMode,
        string? publicationState,
        bool includeArchived,
        PageRequest pageRequest)
    {
        return new CatalogQuery
        {
            ProductId = productId,
            SearchTerm = NullIfBlank(search),
            TrackingMode = ParseTrackingMode(trackingMode),
            PublicationState = ParsePublicationState(publicationState),
            IncludeArchived = includeArchived,
            Page = pageRequest,
        };
    }

    /// <summary>Builds the piece list query.</summary>
    /// <param name="variantId">An optional owning-variant filter.</param>
    /// <param name="search">An optional free-text term matched against piece code and story.</param>
    /// <param name="publicationState">An optional publication-state filter string.</param>
    /// <param name="includeArchived">Whether to include archived pieces.</param>
    /// <param name="pageRequest">The paging/sort intent.</param>
    /// <returns>The typed piece query.</returns>
    /// <exception cref="CatalogContractFormatException">Thrown when <paramref name="publicationState"/> is not a recognized state.</exception>
    public static PieceQuery BuildPieceQuery(
        Guid? variantId,
        string? search,
        string? publicationState,
        bool includeArchived,
        PageRequest pageRequest)
    {
        return new PieceQuery
        {
            VariantId = variantId,
            SearchTerm = NullIfBlank(search),
            PublicationState = ParsePublicationState(publicationState),
            IncludeArchived = includeArchived,
            Page = pageRequest,
        };
    }

    private static PublicationState? ParsePublicationState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (System.Enum.TryParse(value, ignoreCase: true, out PublicationState state) && System.Enum.IsDefined(state))
        {
            return state;
        }

        throw new CatalogContractFormatException(
            $"'{value}' is not a recognized publication state. Expected 'Draft' or 'PublicApproved'.");
    }

    private static TrackingMode? ParseTrackingMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (System.Enum.TryParse(value, ignoreCase: true, out TrackingMode mode) && System.Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new CatalogContractFormatException(
            $"'{value}' is not a recognized tracking mode. Expected 'Serialized' or 'Quantity'.");
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
