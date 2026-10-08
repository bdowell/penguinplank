using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A normalized line of an <see cref="ExternalOrder"/>: the external line identifier, the mapped
/// internal variant (when resolved), quantity, and unit price.
/// </summary>
/// <remarks>
/// <para>
/// Lines are captured and normalized as part of the external-order schema; posting them to an
/// internal sale is designed-only in Phase A (design entity catalog). The optional
/// <see cref="VariantId"/> is populated when the line's external resource is mapped to an internal
/// variant through an <see cref="EntityMapping"/>; it is null while the mapping is unresolved —
/// matched suggestions are reviewed, never applied silently (requirement 3.5).
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: a line is a durable child of its order. The monetary
/// <see cref="UnitPrice"/> uses the model-wide money precision (decimal(19,4)).
/// </para>
/// </remarks>
public class ExternalOrderLine : Entity
{
    /// <summary>The owning <see cref="ExternalOrder"/>.</summary>
    public Guid ExternalOrderId { get; set; }

    /// <summary>The line's identifier on the external platform.</summary>
    public string ExternalLineId { get; set; } = string.Empty;

    /// <summary>The mapped internal variant, when the line's external resource has been mapped.</summary>
    public Guid? VariantId { get; set; }

    /// <summary>The ordered quantity. Stock quantities are whole units.</summary>
    public int Quantity { get; set; }

    /// <summary>The unit price as reported by the platform, in USD (decimal(19,4)).</summary>
    public decimal UnitPrice { get; set; }
}
