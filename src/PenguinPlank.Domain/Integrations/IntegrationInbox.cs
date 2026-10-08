using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A durable record of an inbound provider notification, keyed by a unique delivery key, recorded
/// before acknowledgment and processed asynchronously with lease-based, crash-safe handling.
/// </summary>
/// <remarks>
/// <para>
/// An inbound notification is durably recorded before it is acknowledged to the provider
/// (requirement 3.6 / R11). The <see cref="DeliveryKey"/> is <b>unique</b> (enforced by a unique
/// index) so a repeated or out-of-order delivery is deduplicated and reconciled against
/// authoritative state rather than reapplied (requirement 3.8). The signature state records the
/// outcome of validating the provider's prescribed authentication before the payload is treated
/// as authoritative.
/// </para>
/// <para>
/// The worker leases an item for processing: <see cref="LeaseOwner"/> and
/// <see cref="LeaseExpiresAtUtc"/> allow crash-safe reclaim of an expired lease, and
/// <see cref="Attempts"/> records attempt history. The hot lookup index on
/// (<see cref="ConnectionId"/>, <see cref="Status"/>, <see cref="LeaseExpiresAtUtc"/>) from the
/// design index strategy supports leasing. Connectors are disabled in Phase A, so no item is
/// processed yet.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: an inbox item is updated in place by the worker but its
/// identity and concurrency are governed by the lease fields and the unique delivery key rather
/// than a general-purpose rowversion.
/// </para>
/// </remarks>
public class IntegrationInbox : Entity
{
    /// <summary>The owning <see cref="IntegrationConnection"/>.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>
    /// The provider-supplied delivery key. <b>Unique</b> across all inbox items so repeated or
    /// out-of-order deliveries are deduplicated (requirement 3.8).
    /// </summary>
    public string DeliveryKey { get; set; } = string.Empty;

    /// <summary>A reference to the stored notification payload (held outside this row).</summary>
    public string PayloadReference { get; set; } = string.Empty;

    /// <summary>The outcome of validating the provider's signature or authentication.</summary>
    public string SignatureState { get; set; } = string.Empty;

    /// <summary>The processing status. Defaults to <see cref="InboxStatus.Received"/>.</summary>
    public InboxStatus Status { get; set; } = InboxStatus.Received;

    /// <summary>The number of processing attempts made so far.</summary>
    public int Attempts { get; set; }

    /// <summary>The worker instance currently holding the lease, when leased.</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>The instant the current lease expires, enabling crash-safe reclaim.</summary>
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    /// <summary>The instant the notification was received and durably recorded.</summary>
    public DateTimeOffset ReceivedAtUtc { get; set; }
}
