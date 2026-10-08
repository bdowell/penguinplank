using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Costing;

/// <summary>
/// One material/labor line of a <see cref="CostEstimate"/>: a quantity, a rate, and the
/// extended line cost.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. Costing material
/// quantities may be <b>fractional</b> (design "Global column and type conventions"), so
/// <see cref="Quantity"/> is a decimal; <see cref="Rate"/> uses rate precision and
/// <see cref="LineCost"/> uses money precision.
/// </remarks>
public class CostEstimateLine : VersionedEntity
{
    /// <summary>The owning <see cref="CostEstimate"/>. Required.</summary>
    public Guid CostEstimateId { get; set; }

    /// <summary>A description of the material or labor item.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The quantity consumed (fractional allowed for costing materials).</summary>
    public decimal Quantity { get; set; }

    /// <summary>The per-unit rate (rate precision).</summary>
    public decimal Rate { get; set; }

    /// <summary>The extended line cost (money).</summary>
    public decimal LineCost { get; set; }
}
