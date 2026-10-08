namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned transport shape returned for a single <c>ProductVariant</c> on a list or detail
/// read, including its sizing, pricing, and wood composition.
/// </summary>
/// <remarks>
/// <para>
/// A standalone Contracts DTO with no persistence entity or EF type (dependency rule;
/// coding-standards §3) — the API maps the Application <c>ProductVariantView</c> read model into
/// this shape so an EF navigation graph is never serialized. Prices are USD <see cref="decimal"/>
/// and the currency is stated explicitly via <see cref="Currency"/>; tracking mode is a stable
/// string. The opaque <see cref="ETag"/> is echoed as <c>If-Match</c> on a later edit
/// (requirements 6.5, 6.6).
/// </para>
/// </remarks>
public sealed record VariantResponse
{
    /// <summary>The variant's stable identifier.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The owning product's identifier.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The variant's unique SKU.</summary>
    public required string Sku { get; init; }

    /// <summary>The optional barcode, unique when present.</summary>
    public string? Barcode { get; init; }

    /// <summary>The variant's stock tracking mode as a stable string ("Serialized" or "Quantity").</summary>
    public required string TrackingMode { get; init; }

    /// <summary>The explicit unit the variant's quantity is counted or measured in.</summary>
    public required string UnitOfMeasure { get; init; }

    /// <summary>The variant's sizing with its explicit unit, when supplied.</summary>
    public DimensionsContract? Dimensions { get; init; }

    /// <summary>The optional surface finish description.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional retail price in the stated <see cref="Currency"/>.</summary>
    public decimal? RetailPrice { get; init; }

    /// <summary>The optional wholesale price in the stated <see cref="Currency"/>.</summary>
    public decimal? WholesalePrice { get; init; }

    /// <summary>The ISO 4217 currency code the prices are expressed in. Always "USD" in Phase A.</summary>
    public required string Currency { get; init; }

    /// <summary>The optional number of units in a wholesale case pack.</summary>
    public int? CasePack { get; init; }

    /// <summary>The optional care profile this variant references.</summary>
    public Guid? CareProfileId { get; init; }

    /// <summary>The variant's wood composition; empty when none is recorded.</summary>
    public IReadOnlyList<WoodComponentContract> WoodComposition { get; init; } = [];

    /// <summary>Whether the variant is active; an archived variant rejects new transactions.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The opaque concurrency token to echo as <c>If-Match</c> on a subsequent edit.</summary>
    public required string ETag { get; init; }
}
