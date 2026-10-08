using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A record of one synchronization execution for a connection: its counts, timing, and last
/// success, used to present sync history to the Owner.
/// </summary>
/// <remarks>
/// <para>
/// Sync history exposes the last success, backlog, and failed items for an Owner to review
/// (requirement 3.12 / R11). A run records how many items it processed and failed, when it
/// started and finished, and the last successful instant. Permanent mapping or permission errors
/// raised during a run are routed to the <see cref="SyncException"/> queue (requirement 3.11).
/// Connectors are disabled in Phase A, so no run executes yet.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: a run is a history record, written and updated as it
/// progresses, and must never be cascade-deleted (the model-wide restrict default applies).
/// </para>
/// </remarks>
public class SyncRun : Entity
{
    /// <summary>The owning <see cref="IntegrationConnection"/>.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>The resource the run synchronized (for example, "orders").</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>The number of items processed in this run.</summary>
    public int ProcessedCount { get; set; }

    /// <summary>The number of items that failed in this run.</summary>
    public int FailedCount { get; set; }

    /// <summary>The instant the run started.</summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>The instant the run completed, when finished.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>The instant of the last successful synchronization observed by this run.</summary>
    public DateTimeOffset? LastSuccessAtUtc { get; set; }
}
