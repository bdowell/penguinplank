namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The fully decided set of values a use case hands a <see cref="IChannelListingStore"/> to insert
/// a new <c>ChannelListing</c> reference after its required fields have been accepted.
/// </summary>
/// <remarks>
/// A channel listing is a reference-only record in Phase A (requirement 1.10); the use case
/// validates that the channel, external id, and status are present (requirement 1.9) and that the
/// listed variant is active before constructing this record. The store writes exactly what the use
/// case decided and performs no synchronization. This is an immutable value with no I/O
/// (coding-standards §1).
/// </remarks>
public sealed record NewChannelListingRecord
{
    /// <summary>The identifier the use case generated for the new listing.</summary>
    public required Guid ChannelListingId { get; init; }

    /// <summary>The listed variant.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The accepted external channel name.</summary>
    public required string Channel { get; init; }

    /// <summary>The accepted external listing identifier.</summary>
    public required string ExternalId { get; init; }

    /// <summary>The accepted listing status.</summary>
    public required string Status { get; init; }

    /// <summary>The instant the listing reference was recorded, resolved from the injected time provider.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The optional public URL of the external listing.</summary>
    public string? Url { get; init; }

    /// <summary>The optional readiness state of the listing content.</summary>
    public string? ReadinessState { get; init; }
}
