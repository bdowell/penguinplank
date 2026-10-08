using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A record of an external listing for a <see cref="ProductVariant"/>, retaining the channel,
/// external identifier, URL, status, readiness, and last-verified details.
/// </summary>
/// <remarks>
/// <para>
/// A channel listing is a <b>stored reference only</b> while no connected channel is enabled
/// under the integration foundation (requirement 1.10); Phase A performs no channel
/// synchronization for it. It records <see cref="Channel"/>, <see cref="ExternalId"/>, and
/// <see cref="Status"/> — all required (requirement 1.9) — plus the URL, readiness state, and
/// last-verified timestamp and source.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: a listing is edited (status, verification) but is a
/// lightweight reference record rather than a concurrency-protected aggregate. The
/// required-field validation is a use-case concern added by a later task.
/// </para>
/// </remarks>
public class ChannelListing : Entity
{
    /// <summary>The listed <see cref="ProductVariant"/>. Required.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The external channel name (for example, "Etsy"). Required (requirement 1.9).</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>The listing's identifier on the external channel. Required (requirement 1.9).</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>The public URL of the external listing, when known.</summary>
    public string? Url { get; set; }

    /// <summary>The listing status on the channel. Required (requirement 1.9).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The readiness state of the listing content (for example, "ReadyToList").</summary>
    public string? ReadinessState { get; set; }

    /// <summary>The instant the listing was last verified against the channel, when known.</summary>
    public DateTimeOffset? LastVerifiedAtUtc { get; set; }

    /// <summary>The source of the last verification (for example, "ManualCheck" or a sync run id).</summary>
    public string? LastVerifiedSource { get; set; }
}
