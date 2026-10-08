namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned request body for creating a <c>ProductVariant</c> (a sellable SKU) via
/// <c>POST /api/v1/variants</c>.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO carrying intent only (dependency rule; coding-standards §3).
/// Monetary values are USD <see cref="decimal"/>; sizing carries its explicit unit via
/// <see cref="DimensionsContract"/>; <see cref="TrackingMode"/> is a stable string ("Serialized"
/// or "Quantity"). SKU uniqueness, dimension validity, and wood proportions are validated
/// server-side by the domain policies (requirements 1.1–1.5). A new variant starts in <c>Draft</c>
/// (requirement 1.12).
/// </remarks>
public sealed record CreateVariantContract
{
    /// <summary>The owning product. Required.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The stock-keeping unit code. Required and unique across all variants.</summary>
    public required string Sku { get; init; }

    /// <summary>The stock tracking mode as a stable string ("Serialized" or "Quantity"). Required.</summary>
    public required string TrackingMode { get; init; }

    /// <summary>The explicit unit the variant's quantity is counted or measured in. Required.</summary>
    public required string UnitOfMeasure { get; init; }

    /// <summary>The optional barcode. Unique when supplied.</summary>
    public string? Barcode { get; init; }

    /// <summary>The optional physical sizing with its explicit unit.</summary>
    public DimensionsContract? Dimensions { get; init; }

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

    /// <summary>The optional wood composition. Empty when omitted.</summary>
    public IReadOnlyList<WoodComponentContract> WoodComposition { get; init; } = [];
}
