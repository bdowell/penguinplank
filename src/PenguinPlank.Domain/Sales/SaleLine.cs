using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Sales;

/// <summary>
/// One line of a <see cref="Sale"/>: a variant (and optional serialized piece), quantity,
/// unit price, discount, and a cost snapshot.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. Quantity is whole
/// units; monetary fields use money precision. The variant/piece references are designed-only
/// scalar seams (no FK in Phase A).
/// </remarks>
public class SaleLine : VersionedEntity
{
    /// <summary>The owning <see cref="Sale"/>. Required.</summary>
    public Guid SaleId { get; set; }

    /// <summary>The variant sold (designed-only scalar link to Catalog ProductVariant). Required.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The specific piece sold, for serialized variants (designed-only scalar seam).</summary>
    public Guid? PieceId { get; set; }

    /// <summary>The quantity sold (whole units).</summary>
    public int Quantity { get; set; }

    /// <summary>The unit price charged (money).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>The per-line discount applied (money).</summary>
    public decimal LineDiscount { get; set; }

    /// <summary>A snapshot of the recognized unit cost for the line (money).</summary>
    public decimal CostSnapshot { get; set; }
}
