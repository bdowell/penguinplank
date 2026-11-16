using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Pure state rules for the sync exception queue: which actions are available in a state and the
/// state an action leads to.
/// </summary>
public static class SyncExceptionActions
{
    /// <summary>Lists the actions available for an exception in <paramref name="status"/>.</summary>
    /// <param name="status">The exception's current state.</param>
    /// <returns>Retry and reconcile when open, reconcile only when a retry is pending, none when resolved.</returns>
    public static IReadOnlyList<SyncExceptionAction> Available(SyncExceptionStatus status)
    {
        return status switch
        {
            SyncExceptionStatus.Open => [SyncExceptionAction.Retry, SyncExceptionAction.Reconcile],
            SyncExceptionStatus.RetryRequested => [SyncExceptionAction.Reconcile],
            _ => [],
        };
    }

    /// <summary>Computes the state reached by applying an action.</summary>
    /// <param name="status">The exception's current state.</param>
    /// <param name="action">The requested action.</param>
    /// <returns>The new state, or <see langword="null"/> when the action is not available in <paramref name="status"/>.</returns>
    public static SyncExceptionStatus? Apply(SyncExceptionStatus status, SyncExceptionAction action)
    {
        if (!Available(status).Contains(action))
        {
            return null;
        }

        return action == SyncExceptionAction.Retry
            ? SyncExceptionStatus.RetryRequested
            : SyncExceptionStatus.Resolved;
    }
}
