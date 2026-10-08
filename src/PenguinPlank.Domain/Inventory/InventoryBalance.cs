using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Inventory;

/// <summary>
/// The on-hand quantity and carried cost components of one variant at one location.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). A balance is a mutable aggregate and is unique
/// per (<see cref="VariantId"/>, <see cref="LocationId"/>). <see cref="Quantity"/> is a
/// whole-unit stock count that must stay nonnegative (design invariant 2); the enforcement
/// logic is a later-phase concern. Cost components use money precision.
/// </remarks>
public class InventoryBalance : VersionedEntity
{
    /// <summary>The variant held (designed-only scalar link to Catalog ProductVariant). Required.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The <see cref="Location"/> the stock is held at. Required.</summary>
    public Guid LocationId { get; set; }

    /// <summary>The on-hand quantity in whole units (must stay nonnegative).</summary>
    public int Quantity { get; set; }

    /// <summary>The carried per-unit cost component (money).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>The total carried cost component for the on-hand quantity (money).</summary>
    public decimal TotalCost { get; set; }
}
