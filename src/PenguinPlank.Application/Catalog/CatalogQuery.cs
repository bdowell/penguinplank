using PenguinPlank.Application.Common;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The filter and paging intent for listing <c>ProductVariant</c> read models.
/// </summary>
/// <remarks>
/// <para>
/// An immutable query value carried across the read boundary (the design's "CatalogQuery"). It
/// pairs optional filters with a <see cref="Page"/> request; the reader normalizes the page via
/// the Phase A clamping policy and returns a <see cref="Common.Page{T}"/> with a total count so no
/// <c>IQueryable</c> leaks across the boundary (coding-standards §2, §3; requirement A4 §6.1).
/// Null filter members mean "no restriction".
/// </para>
/// </remarks>
public sealed record CatalogQuery
{
    /// <summary>Restricts results to variants of a single product, when supplied.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>A free-text term matched against SKU and product name, when supplied.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Restricts results to a single tracking mode, when supplied.</summary>
    public TrackingMode? TrackingMode { get; init; }

    /// <summary>Restricts results to a single publication state, when supplied.</summary>
    public PublicationState? PublicationState { get; init; }

    /// <summary>
    /// Whether to include archived (inactive) variants. Defaults to <see langword="false"/> so a
    /// default list shows only active master data; archived records remain readable when
    /// explicitly requested (requirement 1.8).
    /// </summary>
    public bool IncludeArchived { get; init; }

    /// <summary>The paging and stable-sort intent. Defaults to the first page at the default size.</summary>
    public PageRequest Page { get; init; } = PageRequest.Default;
}
