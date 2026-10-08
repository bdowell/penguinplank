using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to create a <c>ProductVariant</c> (a sellable SKU) under an existing
/// product.
/// </summary>
/// <remarks>
/// <para>
/// An immutable request value carried across the catalog write boundary (the design's
/// "VariantDraft"). It expresses the variant's SKU, tracking mode, unit of measure, optional
/// barcode, sizing, finish, pricing, and optional wood composition as ordinary inputs rather than
/// an EF entity, so the use case can validate it with the pure domain policies (SKU uniqueness,
/// dimension validity, wood proportions) and persist it without leaking persistence types
/// (coding-standards §2, §3, §5).
/// </para>
/// <para>
/// Monetary values are USD <c>decimal</c>; sizing carries its explicit unit via
/// <see cref="Dimensions"/> (requirements 1.1, 1.3, 1.4, 1.5). A new variant starts in
/// <c>Draft</c> publication state (requirement 1.12), applied by the use case rather than
/// supplied here.
/// </para>
/// </remarks>
public sealed record CreateVariantRequest
{
    /// <summary>The owning product. Required and non-empty.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The stock-keeping unit code. Required and unique across all variants (requirements 1.1, 1.2).</summary>
    public required string Sku { get; init; }

    /// <summary>Whether pieces are tracked individually or units are counted. Required (requirement 1.1).</summary>
    public required TrackingMode TrackingMode { get; init; }

    /// <summary>The explicit unit the variant's quantity is counted or measured in. Required (requirement 1.1).</summary>
    public required string UnitOfMeasure { get; init; }

    /// <summary>The optional barcode. Unique when supplied (design invariant 1).</summary>
    public string? Barcode { get; init; }

    /// <summary>The optional physical sizing with its explicit unit (requirements 1.3, 1.4).</summary>
    public Dimensions? Dimensions { get; init; }

    /// <summary>The optional surface finish description.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional retail price in USD.</summary>
    public decimal? RetailPrice { get; init; }

    /// <summary>The optional wholesale price in USD.</summary>
    public decimal? WholesalePrice { get; init; }

    /// <summary>The optional number of units in a wholesale case pack.</summary>
    public int? CasePack { get; init; }

    /// <summary>The optional care profile this variant references for care guidance.</summary>
    public Guid? CareProfileId { get; init; }

    /// <summary>
    /// The optional wood composition. Each component's proportion, when supplied, is validated
    /// against the 0–100 range by the domain policy (requirements 1.5, 2.2). Empty when omitted.
    /// </summary>
    public IReadOnlyList<WoodComponent> WoodComposition { get; init; } = [];
}
