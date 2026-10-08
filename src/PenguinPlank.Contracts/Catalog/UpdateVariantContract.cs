namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned request body for editing a <c>ProductVariant</c> via
/// <c>PATCH /api/v1/variants/{id}</c>.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO carrying intent only (dependency rule; coding-standards §3). The
/// concurrency token travels in the HTTP <c>If-Match</c> header, not this body, so a stale edit is
/// rejected (requirements 6.5, 6.6). The SKU and tracking mode are deliberately absent: SKU
/// identity and the tracking-mode change rule are governed by their own guarded operations
/// (requirement 1.13), not a general field update.
/// </remarks>
public sealed record UpdateVariantContract
{
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
}
