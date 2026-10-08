using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Sales;

/// <summary>
/// A sale header: its channel, optional event, external reference, optional contact, and the
/// monetary breakdown (prices, discounts, tax, fees) with a movement reference.
/// </summary>
/// <remarks>
/// <para>
/// Designed-only in Phase A (design Section 6); no services exercise it. A sale is a mutable
/// aggregate. Monetary fields use money precision and USD rounding at transaction boundaries
/// (centralized in the shared MoneyRounding policy). Its lines are <see cref="SaleLine"/> and
/// use the no-cascade-delete default so sale history is never destroyed (design invariant 5).
/// </para>
/// <para>
/// <b>Additive extension seams.</b> <see cref="ContactId"/> and <see cref="CustomerId"/> are
/// documented nullable GUID columns reserving the future customer-foundation link
/// (CustomerIdentity): a sale may later be attributed to a contact or customer. No
/// CustomerIdentity table is created in Phase A — these columns carry <b>no FK constraint</b>
/// and are wired additively when customer foundation is implemented (design "additive
/// extension seams"). <see cref="EventId"/> similarly links to a future Markets Event.
/// </para>
/// </remarks>
public class Sale : VersionedEntity
{
    /// <summary>The sales channel (for example, "Event", "Online", "Wholesale").</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>The event the sale occurred at, if any (designed-only scalar seam; no FK).</summary>
    public Guid? EventId { get; set; }

    /// <summary>An optional external reference (for example, a marketplace order number).</summary>
    public string? ExternalReference { get; set; }

    /// <summary>
    /// Extension seam: the contact the sale is attributed to. A documented nullable GUID with no
    /// FK in Phase A, reserving the future customer-foundation link (CustomerIdentity).
    /// </summary>
    public Guid? ContactId { get; set; }

    /// <summary>
    /// Extension seam: the customer identity the sale is attributed to. A documented nullable
    /// GUID with no FK in Phase A, reserving the future CustomerIdentity table additively.
    /// </summary>
    public Guid? CustomerId { get; set; }

    /// <summary>The sale date (date-only local intent; see A8).</summary>
    public DateTimeOffset SaleDate { get; set; }

    /// <summary>The subtotal before discounts/tax/fees (money).</summary>
    public decimal Subtotal { get; set; }

    /// <summary>The total discount applied (money).</summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>The tax charged (money).</summary>
    public decimal TaxTotal { get; set; }

    /// <summary>The processing/channel fees (money).</summary>
    public decimal FeeTotal { get; set; }

    /// <summary>The grand total collected (money).</summary>
    public decimal GrandTotal { get; set; }

    /// <summary>A snapshot of the recognized cost of goods for the sale (money).</summary>
    public decimal CostSnapshot { get; set; }

    /// <summary>The inventory movement that recorded the stock-out for this sale (designed-only scalar seam).</summary>
    public Guid? MovementId { get; set; }
}
