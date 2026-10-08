using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A durable outgoing integration job, keyed by a unique operation key, created in the same
/// transaction as the business change that requires it and dispatched asynchronously with
/// lease-based, crash-safe handling.
/// </summary>
/// <remarks>
/// <para>
/// When a business change requires an outgoing integration job, the job is created in the
/// <b>same database transaction</b> as the change (transactional outbox; requirement 3.14 / R11),
/// so the job exists if and only if the change committed. The <see cref="OperationKey"/> is
/// <b>unique</b> (enforced by a unique index) so an operation is not enqueued twice.
/// </para>
/// <para>
/// The worker leases a job for dispatch: <see cref="LeaseOwner"/> and
/// <see cref="LeaseExpiresAtUtc"/> allow crash-safe reclaim, and <see cref="Attempts"/> records
/// attempt history. The hot lookup index on
/// (<see cref="ConnectionId"/>, <see cref="Status"/>, <see cref="LeaseExpiresAtUtc"/>) from the
/// design index strategy supports leasing. Connectors are disabled in Phase A, so a job is never
/// dispatched to a real provider.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: an outbox job is updated in place by the worker but its
/// concurrency is governed by the lease fields and the unique operation key.
/// </para>
/// </remarks>
public class IntegrationOutbox : Entity
{
    /// <summary>The owning <see cref="IntegrationConnection"/>.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>
    /// The caller-defined operation key. <b>Unique</b> across all outbox jobs so an operation is
    /// enqueued at most once (requirement 3.14).
    /// </summary>
    public string OperationKey { get; set; } = string.Empty;

    /// <summary>A reference to the stored job payload (held outside this row).</summary>
    public string PayloadReference { get; set; } = string.Empty;

    /// <summary>The processing status. Defaults to <see cref="OutboxStatus.Pending"/>.</summary>
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    /// <summary>The number of dispatch attempts made so far.</summary>
    public int Attempts { get; set; }

    /// <summary>The worker instance currently holding the lease, when leased.</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>The instant the current lease expires, enabling crash-safe reclaim.</summary>
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
}
