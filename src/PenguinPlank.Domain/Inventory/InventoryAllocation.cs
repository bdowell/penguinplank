using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Inventory;

/// <summary>
/// A reservation of stock for an event: a variant (and optional serialized piece), an
/// allocated quantity, and its fulfilled/released state.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The event and
/// variant/piece references are designed-only scalar seams with no FK in Phase A.
/// </remarks>
public class InventoryAllocation : VersionedEntity
{
    /// <summary>The Markets Event the stock is allocated to (designed-only scalar seam). Required.</summary>
    public Guid EventId { get; set; }

    /// <summary>The variant allocated (designed-only scalar link to Catalog ProductVariant). Required.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The specific piece allocated, for serialized variants (designed-only scalar seam).</summary>
    public Guid? PieceId { get; set; }

    /// <summary>The allocated quantity in whole units.</summary>
    public int AllocatedQuantity { get; set; }

    /// <summary>The allocation state (for example, "Reserved", "Fulfilled", "Released").</summary>
    public string? State { get; set; }
}
