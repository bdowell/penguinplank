using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Markets;

/// <summary>
/// One line of a <see cref="PackingTemplate"/>: a referenced product, consumable, or
/// equipment item with a planned quantity and instructions.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). The referenced-entity link is a designed-only
/// scalar seam (its kind is recorded by <see cref="ReferenceType"/>) with no FK in Phase A.
/// </remarks>
public class PackingTemplateItem : Entity
{
    /// <summary>The owning <see cref="PackingTemplate"/>. Required.</summary>
    public Guid PackingTemplateId { get; set; }

    /// <summary>The kind of thing referenced (for example, "Product", "Consumable", "Equipment").</summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>The referenced entity's id (designed-only scalar seam; no FK in Phase A).</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>The planned quantity to pack (whole units).</summary>
    public int PlannedQuantity { get; set; }

    /// <summary>Optional packing instructions for the item.</summary>
    public string? Instructions { get; set; }
}
