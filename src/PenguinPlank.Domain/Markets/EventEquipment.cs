using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Markets;

/// <summary>
/// A reusable piece of event equipment (tables, tent, display furniture) and, where linked,
/// its use at a specific event.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The optional
/// <see cref="EventId"/> links a usage to an event as a designed-only scalar seam (no FK in
/// Phase A); a null event means a catalog entry in the reusable equipment list.
/// </remarks>
public class EventEquipment : VersionedEntity
{
    /// <summary>The equipment name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>An optional description of the equipment.</summary>
    public string? Description { get; set; }

    /// <summary>The event this equipment is assigned to, if any (designed-only scalar seam).</summary>
    public Guid? EventId { get; set; }

    /// <summary>Whether the equipment is active in the reusable catalog.</summary>
    public bool ActiveFlag { get; set; } = true;
}
