using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A stock reservation held against an <see cref="ExternalOrder"/> for a mapped internal variant,
/// with its reserved quantity and fulfillment state.
/// </summary>
/// <remarks>
/// <para>
/// Reservations are part of the external-order capture schema; the logic that converts a
/// reservation into an inventory allocation or sale is designed-only in Phase A (design entity
/// catalog). This entity records the reserved variant, the reserved quantity, and whether the
/// reservation has been fulfilled or released.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: a reservation is a durable child of its order and must
/// never be cascade-deleted along with history (the model-wide restrict default applies).
/// </para>
/// </remarks>
public class OrderReservation : Entity
{
    /// <summary>The owning <see cref="ExternalOrder"/>.</summary>
    public Guid ExternalOrderId { get; set; }

    /// <summary>The mapped internal variant the stock is reserved for.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The reserved quantity. Stock quantities are whole units.</summary>
    public int ReservedQuantity { get; set; }

    /// <summary>The reservation state (for example, "Held", "Fulfilled", "Released").</summary>
    public string State { get; set; } = string.Empty;
}
