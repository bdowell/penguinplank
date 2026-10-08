using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Sales;

/// <summary>
/// An expense recorded against an event: category, amount, optional receipt media, and work
/// hours.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The event and
/// receipt-media references are designed-only scalar seams (no FK in Phase A). The amount uses
/// money precision; work hours use dimension/decimal precision.
/// </remarks>
public class EventExpense : VersionedEntity
{
    /// <summary>The event the expense belongs to (designed-only scalar seam). Required.</summary>
    public Guid EventId { get; set; }

    /// <summary>The expense category (for example, "Booth", "Travel", "Supplies").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The expense amount (money).</summary>
    public decimal Amount { get; set; }

    /// <summary>An optional receipt media asset (designed-only scalar seam; no FK in Phase A).</summary>
    public Guid? ReceiptMediaId { get; set; }

    /// <summary>Work hours attributed to the expense, if any (fractional allowed).</summary>
    public decimal? WorkHours { get; set; }
}
