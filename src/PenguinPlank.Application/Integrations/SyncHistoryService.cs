using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Use cases for viewing sync history and acting on queued sync exceptions (requirements 3.11,
/// 3.12). Business decisions come from <see cref="SyncExceptionActions"/>; this type only
/// orchestrates the store.
/// </summary>
public sealed class SyncHistoryService
{
    private readonly ISyncHistoryStore _historyStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the service over the history store and time source.</summary>
    /// <param name="historyStore">The sync history persistence boundary.</param>
    /// <param name="timeProvider">The time source for modification instants.</param>
    public SyncHistoryService(ISyncHistoryStore historyStore, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(historyStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _historyStore = historyStore;
        _timeProvider = timeProvider;
    }

    /// <summary>Builds the Owner's sync-history view for a connection.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The view, or <see langword="null"/> when the connection does not exist.</returns>
    public async Task<SyncHistoryView?> GetViewAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        SyncHistorySummary? summary = await _historyStore
            .GetSummaryAsync(connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (summary is null)
        {
            return null;
        }

        IReadOnlyList<SyncExceptionRecord> exceptions = await _historyStore
            .ListUnresolvedExceptionsAsync(connectionId, cancellationToken)
            .ConfigureAwait(false);

        List<SyncExceptionView> views = exceptions
            .Select(record => new SyncExceptionView(record, SyncExceptionActions.Available(record.Status)))
            .ToList();

        return new SyncHistoryView(summary, views);
    }

    /// <summary>Applies a retry or reconcile action to a queued exception.</summary>
    /// <param name="exceptionId">The exception.</param>
    /// <param name="action">The requested action.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Success, or a validation failure when the exception is missing or the action is unavailable.</returns>
    public async Task<Result> ApplyActionAsync(
        Guid exceptionId,
        SyncExceptionAction action,
        CancellationToken cancellationToken)
    {
        SyncExceptionRecord? record = await _historyStore
            .GetExceptionAsync(exceptionId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return Result.Failure(ErrorCode.Validation, "The sync exception does not exist.");
        }

        SyncExceptionStatus? next = SyncExceptionActions.Apply(record.Status, action);

        if (next is null)
        {
            return Result.Failure(ErrorCode.Validation, "That action is not available for this sync exception.");
        }

        bool updated = await _historyStore
            .SetExceptionStatusAsync(exceptionId, next.Value, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return updated
            ? Result.Success()
            : Result.Failure(ErrorCode.Validation, "The sync exception does not exist.");
    }
}
