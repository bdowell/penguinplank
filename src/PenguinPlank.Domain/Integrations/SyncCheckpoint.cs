using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A cursor record enabling incremental polling and backfill for one resource of a connection,
/// with periodic reconciliation.
/// </summary>
/// <remarks>
/// <para>
/// A checkpoint records the <see cref="Cursor"/> position reached for a given
/// (<see cref="ConnectionId"/>, <see cref="Resource"/>) so incremental polling resumes where it
/// left off and reconciliation can run periodically (requirement 3.9 / R11). One checkpoint
/// exists per connection + resource, enforced by a unique index in the configuration. Connectors
/// are disabled in Phase A, so no cursor advances yet.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: a checkpoint is updated in place as the cursor advances
/// but is a lightweight position record.
/// </para>
/// </remarks>
public class SyncCheckpoint : Entity
{
    /// <summary>The owning <see cref="IntegrationConnection"/>.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>The resource the cursor tracks (for example, "orders", "products").</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>The opaque cursor/position last reached for this connection + resource.</summary>
    public string? Cursor { get; set; }

    /// <summary>The instant this resource was last reconciled, when reconciliation has run.</summary>
    public DateTimeOffset? LastReconciledAtUtc { get; set; }
}
