using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Markets;

/// <summary>
/// A concrete packing line for a specific <see cref="Event"/>: the planned vs actual
/// quantity and its check state.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The referenced
/// item link is a designed-only scalar seam (no FK in Phase A).
/// </remarks>
public class EventPackingItem : VersionedEntity
{
    /// <summary>The owning <see cref="Event"/>. Required.</summary>
    public Guid EventId { get; set; }

    /// <summary>The kind of thing referenced (for example, "Product", "Consumable", "Equipment").</summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>The referenced entity's id (designed-only scalar seam; no FK in Phase A).</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>The planned quantity (whole units).</summary>
    public int PlannedQuantity { get; set; }

    /// <summary>The actual packed quantity (whole units).</summary>
    public int ActualQuantity { get; set; }

    /// <summary>Whether the item has been checked/confirmed packed.</summary>
    public bool IsChecked { get; set; }
}
