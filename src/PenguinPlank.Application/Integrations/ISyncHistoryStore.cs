using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Persistence boundary for sync run history and the sync exception queue (requirements 3.11,
/// 3.12). History is never deleted by pausing a connection.
/// </summary>
public interface ISyncHistoryStore
{
    /// <summary>Records a new run.</summary>
    /// <param name="run">The run to record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the run is saved.</returns>
    Task BeginRunAsync(NewSyncRun run, CancellationToken cancellationToken);

    /// <summary>Stores the final counts of a run.</summary>
    /// <param name="completion">The completion details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="false"/> when no run matches the id.</returns>
    Task<bool> CompleteRunAsync(SyncRunCompletion completion, CancellationToken cancellationToken);

    /// <summary>Queues a permanent error.</summary>
    /// <param name="exception">The exception to queue.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="false"/> when the referenced run does not exist.</returns>
    Task<bool> QueueExceptionAsync(SyncExceptionDraft exception, CancellationToken cancellationToken);

    /// <summary>Reads one queued exception.</summary>
    /// <param name="exceptionId">The exception identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The exception, or <see langword="null"/> when none matches.</returns>
    Task<SyncExceptionRecord?> GetExceptionAsync(Guid exceptionId, CancellationToken cancellationToken);

    /// <summary>Changes the resolution state of a queued exception.</summary>
    /// <param name="exceptionId">The exception identifier.</param>
    /// <param name="status">The new state.</param>
    /// <param name="nowUtc">The modification instant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="false"/> when no exception matches the id.</returns>
    Task<bool> SetExceptionStatusAsync(
        Guid exceptionId,
        SyncExceptionStatus status,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>Lists a connection's exceptions that are not resolved, newest first.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The unresolved exceptions, at most 200.</returns>
    Task<IReadOnlyList<SyncExceptionRecord>> ListUnresolvedExceptionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken);

    /// <summary>Computes the headline sync-history figures for a connection.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The summary, or <see langword="null"/> when the connection does not exist.</returns>
    Task<SyncHistorySummary?> GetSummaryAsync(Guid connectionId, CancellationToken cancellationToken);
}
