using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Sales;

/// <summary>
/// The end-of-event reconciliation: counted takings, expected balance, variance, and closure
/// resolution.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The event reference
/// is a designed-only scalar seam (no FK in Phase A). Monetary fields use money precision.
/// </remarks>
public class EventReconciliation : VersionedEntity
{
    /// <summary>The event being reconciled (designed-only scalar seam). Required.</summary>
    public Guid EventId { get; set; }

    /// <summary>The counted takings at close (money).</summary>
    public decimal CountedTotal { get; set; }

    /// <summary>The expected balance from recorded sales (money).</summary>
    public decimal ExpectedTotal { get; set; }

    /// <summary>The variance between counted and expected totals (money; may be negative).</summary>
    public decimal Variance { get; set; }

    /// <summary>The closure resolution note explaining any variance.</summary>
    public string? ClosureResolution { get; set; }
}
