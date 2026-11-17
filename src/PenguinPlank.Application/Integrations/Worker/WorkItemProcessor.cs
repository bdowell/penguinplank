namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>
/// The use case for one Background_Worker cycle: lease ready items, hand each to the dispatcher,
/// and record the outcome (requirement 3.15).
/// </summary>
/// <remarks>
/// The caller must establish the system actor (<c>ActorContext.SystemWorker()</c>) for the scope
/// before calling, so the audit interceptor attributes every lease and outcome write to the worker
/// (requirement A7 9.6). A dispatcher exception other than cancellation is a transient failure of
/// that one item and does not stop the cycle.
/// </remarks>
public sealed class WorkItemProcessor
{
    private readonly IWorkLeaseStore _leaseStore;
    private readonly IWorkItemDispatcher _dispatcher;
    private readonly IRetryScheduler _retryScheduler;
    private readonly TimeProvider _timeProvider;
    private readonly WorkerOptions _options;

    /// <summary>Creates the processor.</summary>
    /// <param name="leaseStore">The lease persistence boundary.</param>
    /// <param name="dispatcher">The connector boundary.</param>
    /// <param name="retryScheduler">The retry decision boundary.</param>
    /// <param name="timeProvider">The time source.</param>
    /// <param name="options">The worker settings; validated here.</param>
    public WorkItemProcessor(
        IWorkLeaseStore leaseStore,
        IWorkItemDispatcher dispatcher,
        IRetryScheduler retryScheduler,
        TimeProvider timeProvider,
        WorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(leaseStore);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(retryScheduler);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _leaseStore = leaseStore;
        _dispatcher = dispatcher;
        _retryScheduler = retryScheduler;
        _timeProvider = timeProvider;
        _options = options;
    }

    /// <summary>Runs one processing cycle.</summary>
    /// <param name="leaseOwner">The worker instance identifier recorded on each lease.</param>
    /// <param name="cancellationToken">A token to cancel the cycle.</param>
    /// <returns>The counts for the cycle.</returns>
    public async Task<WorkBatchResult> ProcessBatchAsync(string leaseOwner, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        IReadOnlyList<LeasedWorkItem> items = await _leaseStore
            .LeaseNextAsync(
                leaseOwner,
                now,
                now + _options.LeaseDuration,
                _options.BatchSize,
                _retryScheduler.MaxTotalAttempts,
                cancellationToken)
            .ConfigureAwait(false);

        int succeeded = 0;
        int rescheduled = 0;
        int released = 0;

        foreach (LeasedWorkItem item in items)
        {
            DispatchOutcome outcome = await DispatchAsync(item, cancellationToken).ConfigureAwait(false);
            DateTimeOffset completedAt = _timeProvider.GetUtcNow();

            switch (outcome)
            {
                case DispatchOutcome.Succeeded:
                    await _leaseStore.CompleteAsync(item, leaseOwner, completedAt, cancellationToken).ConfigureAwait(false);
                    succeeded++;
                    break;

                case DispatchOutcome.ConnectorDisabled:
                    await _leaseStore.ReleaseAsync(item, leaseOwner, completedAt, cancellationToken).ConfigureAwait(false);
                    released++;
                    break;

                default:
                    await RescheduleAsync(item, leaseOwner, completedAt, cancellationToken).ConfigureAwait(false);
                    rescheduled++;
                    break;
            }
        }

        return new WorkBatchResult(items.Count, succeeded, rescheduled, released);
    }

    private async Task<DispatchOutcome> DispatchAsync(LeasedWorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            return await _dispatcher.DispatchAsync(item, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A connector fault is handled as a transient failure of this item only.
            return DispatchOutcome.TransientFailure;
        }
    }

    private Task<bool> RescheduleAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        // Exhausted attempts stay failed with no retry time; the lease query skips them.
        TimeSpan? delay = _retryScheduler.NextDelay(item.AttemptNumber);
        DateTimeOffset retryAt = delay is null ? DateTimeOffset.MaxValue : nowUtc + delay.Value;

        return _leaseStore.RescheduleAsync(item, leaseOwner, retryAt, nowUtc, cancellationToken);
    }
}
