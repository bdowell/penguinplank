namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to record a <c>ChannelListing</c> — a stored reference to a variant's
/// listing on an external channel.
/// </summary>
/// <remarks>
/// <para>
/// A channel listing is a <b>reference record only</b> in Phase A: no channel synchronization runs
/// for it while no connected channel is enabled under the integration foundation (requirement
/// 1.10). The <see cref="Channel"/>, <see cref="ExternalId"/>, and <see cref="Status"/> are
/// required (requirement 1.9); the URL, readiness state, and verification details are optional. The
/// use case validates the required fields and records the listing against an active variant. This
/// is an immutable request value with no I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record RecordChannelListingRequest
{
    /// <summary>The listed variant. Required and non-empty.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The external channel name (for example "Etsy"). Required (requirement 1.9).</summary>
    public required string Channel { get; init; }

    /// <summary>The listing's identifier on the external channel. Required (requirement 1.9).</summary>
    public required string ExternalId { get; init; }

    /// <summary>The listing status on the channel. Required (requirement 1.9).</summary>
    public required string Status { get; init; }

    /// <summary>The public URL of the external listing, when known.</summary>
    public string? Url { get; init; }

    /// <summary>The readiness state of the listing content (for example "ReadyToList"), when known.</summary>
    public string? ReadinessState { get; init; }
}
