using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A sellable SKU belonging to a <see cref="Product"/>. A variant declares a serialized or
/// quantity-based <see cref="TrackingMode"/> and carries its sizing, finish, pricing, and
/// care association.
/// </summary>
/// <remarks>
/// <para>
/// A variant is a <b>mutable aggregate</b> and derives from <see cref="VersionedEntity"/>.
/// Its <see cref="Sku"/> is <b>unique</b> across all variants and its <see cref="Barcode"/>
/// is unique when supplied (requirements 1.1, 1.2; design invariant 1) — those constraints
/// live in the entity configuration as a unique index and a filtered unique index.
/// </para>
/// <para>
/// Dimensions are non-negative decimals with an explicit <see cref="DimensionUnit"/>; a round
/// product may supply <see cref="Diameter"/> in place of <see cref="Length"/>/<see cref="Width"/>
/// (requirements 1.3, 1.4). Prices are stored as <c>decimal(19,4)</c> money. Validation of
/// those rules is a pure domain policy added by a later task; this type declares the shape.
/// </para>
/// </remarks>
public class ProductVariant : VersionedEntity
{
    /// <summary>The owning <see cref="Product"/>. Required; references a non-archived product.</summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// The stock-keeping unit code. Required and <b>unique across all variants</b>
    /// (requirements 1.1, 1.2); enforced by a unique index.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// An optional barcode. <b>Unique when supplied</b> (design invariant 1); enforced by a
    /// filtered unique index that ignores null values so many variants may have no barcode.
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Whether individual pieces are tracked (<see cref="TrackingMode.Serialized"/>) or units
    /// are counted (<see cref="TrackingMode.Quantity"/>). Required (requirement 1.1).
    /// </summary>
    public TrackingMode TrackingMode { get; set; }

    /// <summary>
    /// The explicit unit in which the variant's quantity is counted or measured. Required and
    /// non-empty (requirement 1.1).
    /// </summary>
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>The length dimension. Non-negative when supplied; paired with <see cref="DimensionUnit"/>.</summary>
    public decimal? Length { get; set; }

    /// <summary>The width dimension. Non-negative when supplied; paired with <see cref="DimensionUnit"/>.</summary>
    public decimal? Width { get; set; }

    /// <summary>The thickness/height dimension. Non-negative when supplied.</summary>
    public decimal? Thickness { get; set; }

    /// <summary>
    /// The diameter for a round product, supplied in place of <see cref="Length"/>/<see cref="Width"/>
    /// (requirement 1.4). Non-negative when supplied.
    /// </summary>
    public decimal? Diameter { get; set; }

    /// <summary>
    /// The explicit unit for the dimension values (for example, "in" or "cm"). Required when
    /// any dimension is supplied (requirement 1.3).
    /// </summary>
    public string? DimensionUnit { get; set; }

    /// <summary>The surface finish description (for example, "food-safe oil").</summary>
    public string? Finish { get; set; }

    /// <summary>The retail price in USD. Stored as <c>decimal(19,4)</c>.</summary>
    public decimal? RetailPrice { get; set; }

    /// <summary>The wholesale price in USD. Stored as <c>decimal(19,4)</c>.</summary>
    public decimal? WholesalePrice { get; set; }

    /// <summary>The number of units in a wholesale case pack, when sold by the case.</summary>
    public int? CasePack { get; set; }

    /// <summary>
    /// The optional <see cref="CareProfile"/> this variant references for care guidance. A
    /// produced piece preserves the specific <see cref="CareProfileVersion"/> in effect at its
    /// production time (requirement 2.4) rather than this profile pointer.
    /// </summary>
    public Guid? CareProfileId { get; set; }

    /// <summary>
    /// Whether the variant is active. An archived variant remains readable but rejects new
    /// transactions (requirement 1.8).
    /// </summary>
    public bool ActiveFlag { get; set; } = true;
}
