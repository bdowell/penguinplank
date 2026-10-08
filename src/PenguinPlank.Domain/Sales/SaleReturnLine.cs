using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Sales;

/// <summary>
/// One line of a <see cref="SaleReturn"/>: the original sale line, returned quantity, refund,
/// and restock disposition.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The original
/// sale-line reference is a designed-only scalar seam (no FK in Phase A). Quantity is whole
/// units; the refund uses money precision.
/// </remarks>
public class SaleReturnLine : VersionedEntity
{
    /// <summary>The owning <see cref="SaleReturn"/>. Required.</summary>
    public Guid SaleReturnId { get; set; }

    /// <summary>The original <see cref="SaleLine"/> being returned (designed-only scalar seam).</summary>
    public Guid OriginalSaleLineId { get; set; }

    /// <summary>The quantity returned (whole units).</summary>
    public int Quantity { get; set; }

    /// <summary>The refund amount for the line (money).</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>The restock disposition (for example, "Restocked", "Scrapped", "Quarantined").</summary>
    public string? RestockDisposition { get; set; }
}
