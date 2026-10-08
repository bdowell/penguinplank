using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Inventory;

/// <summary>
/// An append-only ledger header recording a stock movement: its type, date, actor, reason,
/// related command/sale, and source/destination locations.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). The inventory ledger is <b>append-only</b>:
/// movements are never mutated after insert, so this type derives from <see cref="Entity"/>
/// (no concurrency token). Its lines are <see cref="InventoryMovementLine"/>. The
/// source/destination and related-sale links are designed-only scalar seams with no FK in
/// Phase A, and all foreign keys use the no-cascade-delete default so ledger history survives
/// parent deletes (design invariant 5).
/// </remarks>
public class InventoryMovement : Entity
{
    /// <summary>The movement type (for example, "Receipt", "Transfer", "Sale", "Adjustment").</summary>
    public string MovementType { get; set; } = string.Empty;

    /// <summary>The date the movement occurred (date-only intent; see A8 time handling).</summary>
    public DateTimeOffset MovementDate { get; set; }

    /// <summary>The actor who recorded the movement (designed-only scalar; no FK in Phase A).</summary>
    public Guid? ActorId { get; set; }

    /// <summary>An optional free-form reason for the movement.</summary>
    public string? Reason { get; set; }

    /// <summary>The related sale, when the movement was driven by a sale (designed-only scalar seam).</summary>
    public Guid? RelatedSaleId { get; set; }

    /// <summary>The source <see cref="Location"/>, when applicable (designed-only scalar seam).</summary>
    public Guid? SourceLocationId { get; set; }

    /// <summary>The destination <see cref="Location"/>, when applicable (designed-only scalar seam).</summary>
    public Guid? DestinationLocationId { get; set; }
}
