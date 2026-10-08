namespace PenguinPlank.Domain.Inventory;

/// <summary>
/// The kind of place inventory can be held.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). Values are explicit so the stored column is
/// stable; later phases add behavior.
/// </remarks>
public enum LocationType
{
    /// <summary>The maker's workshop (default home location).</summary>
    Workshop = 0,

    /// <summary>A market/event location, optionally tied to a specific event.</summary>
    Event = 1,

    /// <summary>A quarantine location for stock withheld from sale.</summary>
    Quarantine = 2,
}
