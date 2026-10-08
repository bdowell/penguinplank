using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Channel;

/// <summary>
/// A per-channel stock policy: pool eligibility, buffer, quotas, exclusive piece assignment,
/// and the authority policy governing stock sharing with a channel.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6); no services exercise it. It is a mutable
/// aggregate. The channel/connection reference is a designed-only scalar seam (no FK in Phase
/// A). Buffer and quota quantities are whole units.
/// </remarks>
public class ChannelStockPolicy : VersionedEntity
{
    /// <summary>The channel/connection the policy governs (designed-only scalar seam). Required.</summary>
    public Guid ChannelId { get; set; }

    /// <summary>Whether stock is eligible to share into this channel's pool.</summary>
    public bool PoolEligible { get; set; }

    /// <summary>The safety buffer withheld from the channel (whole units).</summary>
    public int BufferQuantity { get; set; }

    /// <summary>An optional maximum quota exposed to the channel (whole units).</summary>
    public int? QuotaQuantity { get; set; }

    /// <summary>Whether pieces are assigned exclusively to this channel.</summary>
    public bool ExclusivePieceAssignment { get; set; }

    /// <summary>The authority policy governing who wins on conflicting stock updates.</summary>
    public string? AuthorityPolicy { get; set; }
}
