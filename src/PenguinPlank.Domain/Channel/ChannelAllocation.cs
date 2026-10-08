using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Channel;

/// <summary>
/// A concrete allocation of a variant's stock to a channel under a
/// <see cref="ChannelStockPolicy"/>.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The variant/piece
/// references are designed-only scalar seams (no FK in Phase A). Quantities are whole units.
/// </remarks>
public class ChannelAllocation : VersionedEntity
{
    /// <summary>The governing <see cref="ChannelStockPolicy"/>. Required.</summary>
    public Guid ChannelStockPolicyId { get; set; }

    /// <summary>The variant allocated (designed-only scalar link to Catalog ProductVariant). Required.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The specific piece allocated, for serialized variants (designed-only scalar seam).</summary>
    public Guid? PieceId { get; set; }

    /// <summary>The allocated quantity (whole units).</summary>
    public int AllocatedQuantity { get; set; }
}
