using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// A read model describing one <c>ProductVariant</c> for an authenticated catalog list or detail
/// view, including its sizing, pricing, and wood composition.
/// </summary>
/// <remarks>
/// <para>
/// A flat, immutable snapshot built by the reader from a materialized query — no
/// <c>IQueryable</c>, <c>DbContext</c>, or EF entity crosses the boundary (coding-standards §2,
/// §3). This is the full authenticated read model used by catalog editing surfaces. It is distinct
/// from the role-scoped <c>CatalogVariantView</c>/<c>OwnerCatalogVariantView</c> projection shapes,
/// which the API uses to enforce the Staff/Owner financial allowlist (requirement A2 §5.10,
/// §5.11); the API maps from this read model into the appropriate role shape. It carries the opaque
/// <see cref="ETagToken"/> for a subsequent <c>If-Match</c> edit (requirements 6.5, 6.6).
/// </para>
/// </remarks>
public sealed record ProductVariantView
{
    /// <summary>The variant's stable identifier.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The owning product's identifier.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The variant's unique SKU.</summary>
    public required string Sku { get; init; }

    /// <summary>The optional barcode, unique when present.</summary>
    public string? Barcode { get; init; }

    /// <summary>The variant's stock tracking mode.</summary>
    public required TrackingMode TrackingMode { get; init; }

    /// <summary>The explicit unit the variant's quantity is counted or measured in.</summary>
    public required string UnitOfMeasure { get; init; }

    /// <summary>The variant's sizing with its explicit unit, when supplied.</summary>
    public Dimensions? Dimensions { get; init; }

    /// <summary>The optional surface finish description.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional retail price in USD.</summary>
    public decimal? RetailPrice { get; init; }

    /// <summary>The optional wholesale price in USD.</summary>
    public decimal? WholesalePrice { get; init; }

    /// <summary>The optional number of units in a wholesale case pack.</summary>
    public int? CasePack { get; init; }

    /// <summary>The optional care profile this variant references.</summary>
    public Guid? CareProfileId { get; init; }

    /// <summary>The variant's wood composition; empty when none is recorded.</summary>
    public IReadOnlyList<WoodComponent> WoodComposition { get; init; } = [];

    /// <summary>Whether the variant is active; an archived variant rejects new transactions (requirement 1.8).</summary>
    public required bool IsActive { get; init; }

    /// <summary>The opaque concurrency token (encoded ETag) to echo on a subsequent edit.</summary>
    public required string ETagToken { get; init; }
}
