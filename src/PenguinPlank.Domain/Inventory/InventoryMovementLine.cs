using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Inventory;

/// <summary>
/// One append-only line of an <see cref="InventoryMovement"/>: a variant, optional piece,
/// quantity, and carried cost.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). Like its header, a movement line is
/// append-only and derives from <see cref="Entity"/>. <see cref="Quantity"/> is whole units;
/// <see cref="CarriedCost"/> uses money precision. The variant/piece references are
/// designed-only scalar seams (no FK in Phase A).
/// </remarks>
public class InventoryMovementLine : Entity
{
    /// <summary>The owning <see cref="InventoryMovement"/>. Required.</summary>
    public Guid InventoryMovementId { get; set; }

    /// <summary>The variant moved (designed-only scalar link to Catalog ProductVariant). Required.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The specific piece moved, for serialized variants (designed-only scalar seam).</summary>
    public Guid? PieceId { get; set; }

    /// <summary>The quantity moved in whole units (positive per design invariant 2).</summary>
    public int Quantity { get; set; }

    /// <summary>The carried cost associated with the moved quantity (money).</summary>
    public decimal CarriedCost { get; set; }
}
