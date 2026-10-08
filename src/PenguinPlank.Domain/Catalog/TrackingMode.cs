namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The per-variant declaration of how stock for a <see cref="ProductVariant"/> is tracked.
/// </summary>
/// <remarks>
/// Every variant declares exactly one tracking mode (requirement 1.1). The mode cannot be
/// changed once stock history exists for the variant, absent a designed migration
/// (requirement 1.13 / design invariant 6); that rule is a pure domain policy added by a
/// later task — this enum only names the two legal modes.
/// </remarks>
public enum TrackingMode
{
    /// <summary>
    /// Individual physical pieces are tracked one by one. A serialized variant owns
    /// <see cref="ProductPiece"/> records, each a distinct item that cannot be allocated or
    /// sold twice.
    /// </summary>
    Serialized = 0,

    /// <summary>
    /// Interchangeable units are tracked by count rather than as individually identified
    /// pieces.
    /// </summary>
    Quantity = 1,
}
