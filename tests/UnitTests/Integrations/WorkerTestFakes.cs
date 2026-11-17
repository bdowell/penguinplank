using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Integrations.Worker;

namespace UnitTests.Integrations;

/// <summary>A recorded call to <see cref="IWorkLeaseStore.LeaseNextAsync"/>.</summary>
internal sealed record LeaseCall(
    string LeaseOwner,
    DateTimeOffset NowUtc,
    DateTimeOffset LeaseExpiresAtUtc,
    int BatchSize,
    int MaxTotalAttempts,
    ActorContext? Actor);

/// <summary>A recorded outcome write on <see cref="IWorkLeaseStore"/>.</summary>
internal sealed record OutcomeCall(
    string Method,
    LeasedWorkItem Item,
    string LeaseOwner,
    DateTimeOffset NowUtc,
    DateTimeOffset? RetryAtUtc,
    ActorContext? Actor);

/// <summary>
/// A hand-written <see cref="IWorkLeaseStore"/> that returns a scripted batch and records every call,
/// including the actor visible at the moment of the call.
/// </summary>
internal sealed class RecordingWorkLeaseStore : IWorkLeaseStore
{
    private readonly Func<ActorContext?> _currentActor;
    private readonly IReadOnlyList<LeasedWorkItem> _batch;

    public RecordingWorkLeaseStore(IReadOnlyList<LeasedWorkItem> batch, Func<ActorContext?>? currentActor = null)
    {
        _batch = batch;
        _currentActor = currentActor ?? (() => null);
    }

    public List<LeaseCall> Leases { get; } = [];

    public List<OutcomeCall> Outcomes { get; } = [];

    public Task<IReadOnlyList<LeasedWorkItem>> LeaseNextAsync(
        string leaseOwner,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        int batchSize,
        int maxTotalAttempts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Leases.Add(new LeaseCall(leaseOwner, nowUtc, leaseExpiresAtUtc, batchSize, maxTotalAttempts, _currentActor()));
        return Task.FromResult(_batch);
    }

    public Task<bool> CompleteAsync(
        LeasedWorkItem item, string leaseOwner, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        Outcomes.Add(new OutcomeCall(nameof(CompleteAsync), item, leaseOwner, nowUtc, null, _currentActor()));
        return Task.FromResult(true);
    }

    public Task<bool> RescheduleAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset retryAtUtc,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Outcomes.Add(new OutcomeCall(nameof(RescheduleAsync), item, leaseOwner, nowUtc, retryAtUtc, _currentActor()));
        return Task.FromResult(true);
    }

    public Task<bool> ReleaseAsync(
        LeasedWorkItem item, string leaseOwner, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        Outcomes.Add(new OutcomeCall(nameof(ReleaseAsync), item, leaseOwner, nowUtc, null, _currentActor()));
        return Task.FromResult(true);
    }
}

/// <summary>An <see cref="IWorkItemDispatcher"/> whose result for each item the test scripts.</summary>
internal sealed class ScriptedDispatcher : IWorkItemDispatcher
{
    private readonly Func<LeasedWorkItem, DispatchOutcome> _script;
    private readonly Func<ActorContext?> _currentActor;

    public ScriptedDispatcher(Func<LeasedWorkItem, DispatchOutcome> script, Func<ActorContext?>? currentActor = null)
    {
        _script = script;
        _currentActor = currentActor ?? (() => null);
    }

    public List<LeasedWorkItem> Dispatched { get; } = [];

    public List<ActorContext?> ActorsSeen { get; } = [];

    public Task<DispatchOutcome> DispatchAsync(LeasedWorkItem item, CancellationToken cancellationToken)
    {
        Dispatched.Add(item);
        ActorsSeen.Add(_currentActor());
        return Task.FromResult(_script(item));
    }
}

/// <summary>An <see cref="IRetryScheduler"/> with a fixed delay and a fixed attempt limit.</summary>
internal sealed class FixedRetryScheduler : IRetryScheduler
{
    private readonly TimeSpan _delay;

    public FixedRetryScheduler(int maxTotalAttempts, TimeSpan delay)
    {
        MaxTotalAttempts = maxTotalAttempts;
        _delay = delay;
    }

    public int MaxTotalAttempts { get; }

    public TimeSpan? NextDelay(int attemptsMade) => attemptsMade < MaxTotalAttempts ? _delay : null;
}
