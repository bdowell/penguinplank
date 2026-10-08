using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Costing;

/// <summary>
/// A versioned cost estimate scoped to a variant or piece: labor, waste, overhead, approval,
/// and totals.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6); no services exercise it. It is a mutable
/// aggregate. The variant/piece scope references are designed-only scalar seams (no FK in
/// Phase A). Monetary fields use money precision; its lines are <see cref="CostEstimateLine"/>
/// and use the no-cascade-delete default so costing history is preserved (design invariant 5).
/// </remarks>
public class CostEstimate : VersionedEntity
{
    /// <summary>The estimate version number (increments as the estimate is revised).</summary>
    public int Version { get; set; }

    /// <summary>The variant this estimate scopes, if any (designed-only scalar seam).</summary>
    public Guid? VariantId { get; set; }

    /// <summary>The piece this estimate scopes, if any (designed-only scalar seam).</summary>
    public Guid? PieceId { get; set; }

    /// <summary>The labor cost component (money).</summary>
    public decimal LaborCost { get; set; }

    /// <summary>The waste allowance component (money).</summary>
    public decimal WasteCost { get; set; }

    /// <summary>The overhead component (money).</summary>
    public decimal OverheadCost { get; set; }

    /// <summary>The computed total estimated cost (money).</summary>
    public decimal TotalCost { get; set; }

    /// <summary>Whether the estimate has been approved.</summary>
    public bool IsApproved { get; set; }
}
