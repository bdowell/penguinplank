using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Inventory;

/// <summary>
/// A place inventory can be held: the workshop, an event, or a quarantine area.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). A location has a unique
/// <see cref="LocationCode"/> and an optional <see cref="EventId"/> seam linking an event
/// location to a future Markets Event (no FK constraint in Phase A). It is a mutable
/// aggregate.
/// </remarks>
public class Location : VersionedEntity
{
    /// <summary>A unique business code identifying the location.</summary>
    public string LocationCode { get; set; } = string.Empty;

    /// <summary>The human-readable location name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The kind of location.</summary>
    public LocationType Type { get; set; } = LocationType.Workshop;

    /// <summary>
    /// The optional Markets Event this location belongs to, when <see cref="Type"/> is
    /// <see cref="LocationType.Event"/>. A designed-only nullable scalar seam (no FK in Phase A).
    /// </summary>
    public Guid? EventId { get; set; }

    /// <summary>Whether the location is active.</summary>
    public bool ActiveFlag { get; set; } = true;
}
