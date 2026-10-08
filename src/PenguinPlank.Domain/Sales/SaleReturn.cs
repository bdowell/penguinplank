using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Sales;

/// <summary>
/// A return against a <see cref="Sale"/>: its refund amount and the compensating inventory
/// movement.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. Its lines are
/// <see cref="SaleReturnLine"/> and use the no-cascade-delete default so return history is
/// preserved (design invariant 5).
/// </remarks>
public class SaleReturn : VersionedEntity
{
    /// <summary>The original <see cref="Sale"/> being returned against. Required.</summary>
    public Guid SaleId { get; set; }

    /// <summary>The date the return was recorded (date-only local intent; see A8).</summary>
    public DateTimeOffset ReturnDate { get; set; }

    /// <summary>The total refund amount (money).</summary>
    public decimal RefundTotal { get; set; }

    /// <summary>The compensating inventory movement for the restock (designed-only scalar seam).</summary>
    public Guid? CompensatingMovementId { get; set; }
}
