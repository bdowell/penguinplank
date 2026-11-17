using Microsoft.Extensions.Time.Testing;
using PenguinPlank.Application.Integrations.Worker;

namespace UnitTests.Integrations;

/// <summary>
/// Use-case tests for one Background_Worker cycle (requirement 3.15). Time comes from a
/// <see cref="FakeTimeProvider"/>; the lease store, dispatcher and retry scheduler are hand-written
/// fakes.
/// </summary>
public class WorkItemProcessorTests
{
    private const string Owner = "worker-1";

    private static readonly DateTimeOffset s_start = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly WorkerOptions s_options = new(
        PollInterval: TimeSpan.FromSeconds(30),
        LeaseDuration: TimeSpan.FromMinutes(5),
        BatchSize: 7);

    [Fact]
    public async Task ProcessBatchAsync_AnyBatch_LeasesWithNowLeaseExpiryBatchSizeAndAttemptLimitAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        var store = new RecordingWorkLeaseStore([]);
        WorkItemProcessor processor = CreateProcessor(store, _ => DispatchOutcome.Succeeded, clock, maxTotalAttempts: 4);

        WorkBatchResult result = await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        LeaseCall call = Assert.Single(store.Leases);
        Assert.Equal(Owner, call.LeaseOwner);
        Assert.Equal(s_start, call.NowUtc);
        Assert.Equal(s_start + s_options.LeaseDuration, call.LeaseExpiresAtUtc);
        Assert.Equal(s_options.BatchSize, call.BatchSize);
        Assert.Equal(4, call.MaxTotalAttempts);
        Assert.Equal(new WorkBatchResult(0, 0, 0, 0), result);
    }

    [Fact]
    public async Task ProcessBatchAsync_LaterCycle_LeasesFromTheAdvancedClockAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        var store = new RecordingWorkLeaseStore([]);
        WorkItemProcessor processor = CreateProcessor(store, _ => DispatchOutcome.Succeeded, clock);

        await processor.ProcessBatchAsync(Owner, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(10));
        await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        Assert.Equal(s_start, store.Leases[0].NowUtc);
        Assert.Equal(s_start.AddMinutes(10), store.Leases[1].NowUtc);
        Assert.Equal(s_start.AddMinutes(15), store.Leases[1].LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task ProcessBatchAsync_SucceededItem_CompletesWithCompletionInstantAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        LeasedWorkItem item = Item(WorkItemKind.Inbox, attemptNumber: 1);
        var store = new RecordingWorkLeaseStore([item]);
        var dispatcher = new ScriptedDispatcher(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(3));
            return DispatchOutcome.Succeeded;
        });
        var processor = new WorkItemProcessor(store, dispatcher, new FixedRetryScheduler(3, TimeSpan.FromMinutes(1)), clock, s_options);

        WorkBatchResult result = await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        OutcomeCall outcome = Assert.Single(store.Outcomes);
        Assert.Equal(nameof(IWorkLeaseStore.CompleteAsync), outcome.Method);
        Assert.Equal(item, outcome.Item);
        Assert.Equal(Owner, outcome.LeaseOwner);
        Assert.Equal(s_start.AddSeconds(3), outcome.NowUtc);
        Assert.Equal(new WorkBatchResult(1, 1, 0, 0), result);
    }

    [Fact]
    public async Task ProcessBatchAsync_TransientFailure_ReschedulesAfterBackoffDelayAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        LeasedWorkItem item = Item(WorkItemKind.Outbox, attemptNumber: 2);
        var store = new RecordingWorkLeaseStore([item]);
        WorkItemProcessor processor = CreateProcessor(store, _ => DispatchOutcome.TransientFailure, clock, maxTotalAttempts: 5);

        WorkBatchResult result = await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        OutcomeCall outcome = Assert.Single(store.Outcomes);
        Assert.Equal(nameof(IWorkLeaseStore.RescheduleAsync), outcome.Method);
        Assert.Equal(s_start + TimeSpan.FromMinutes(1), outcome.RetryAtUtc);
        Assert.Equal(new WorkBatchResult(1, 0, 1, 0), result);
    }

    [Fact]
    public async Task ProcessBatchAsync_AttemptsExhausted_SchedulesNoRetryAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        LeasedWorkItem item = Item(WorkItemKind.Inbox, attemptNumber: 3);
        var store = new RecordingWorkLeaseStore([item]);
        WorkItemProcessor processor = CreateProcessor(store, _ => DispatchOutcome.TransientFailure, clock, maxTotalAttempts: 3);

        await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        OutcomeCall outcome = Assert.Single(store.Outcomes);
        Assert.Equal(DateTimeOffset.MaxValue, outcome.RetryAtUtc);
    }

    [Fact]
    public async Task ProcessBatchAsync_ConnectorDisabled_ReleasesWithoutCountingAnAttemptAsync()
    {
        LeasedWorkItem item = Item(WorkItemKind.Outbox, attemptNumber: 1);
        var store = new RecordingWorkLeaseStore([item]);
        WorkItemProcessor processor = CreateProcessor(store, _ => DispatchOutcome.ConnectorDisabled, new FakeTimeProvider(s_start));

        WorkBatchResult result = await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        Assert.Equal(nameof(IWorkLeaseStore.ReleaseAsync), Assert.Single(store.Outcomes).Method);
        Assert.Equal(new WorkBatchResult(1, 0, 0, 1), result);
    }

    [Fact]
    public async Task ProcessBatchAsync_DispatcherThrows_ReschedulesThatItemAndContinuesAsync()
    {
        LeasedWorkItem faulty = Item(WorkItemKind.Inbox, attemptNumber: 1);
        LeasedWorkItem healthy = Item(WorkItemKind.Outbox, attemptNumber: 1);
        var store = new RecordingWorkLeaseStore([faulty, healthy]);
        var dispatcher = new ScriptedDispatcher(item => item.Id == faulty.Id
            ? throw new InvalidOperationException("connector fault")
            : DispatchOutcome.Succeeded);
        var processor = new WorkItemProcessor(
            store, dispatcher, new FixedRetryScheduler(3, TimeSpan.FromMinutes(1)), new FakeTimeProvider(s_start), s_options);

        WorkBatchResult result = await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        Assert.Equal(
            [nameof(IWorkLeaseStore.RescheduleAsync), nameof(IWorkLeaseStore.CompleteAsync)],
            store.Outcomes.Select(outcome => outcome.Method));
        Assert.Equal(new WorkBatchResult(2, 1, 1, 0), result);
    }

    [Fact]
    public async Task ProcessBatchAsync_MixedBatch_CountsEachOutcomeAsync()
    {
        LeasedWorkItem ok = Item(WorkItemKind.Inbox, 1);
        LeasedWorkItem failed = Item(WorkItemKind.Inbox, 1);
        LeasedWorkItem disabled = Item(WorkItemKind.Outbox, 1);
        var store = new RecordingWorkLeaseStore([ok, failed, disabled]);
        WorkItemProcessor processor = CreateProcessor(
            store,
            item => item.Id == ok.Id ? DispatchOutcome.Succeeded
                : item.Id == failed.Id ? DispatchOutcome.TransientFailure
                : DispatchOutcome.ConnectorDisabled,
            new FakeTimeProvider(s_start));

        WorkBatchResult result = await processor.ProcessBatchAsync(Owner, CancellationToken.None);

        Assert.Equal(new WorkBatchResult(3, 1, 1, 1), result);
    }

    [Fact]
    public async Task ProcessBatchAsync_CancelledToken_PropagatesCancellationAsync()
    {
        var store = new RecordingWorkLeaseStore([]);
        WorkItemProcessor processor = CreateProcessor(store, _ => DispatchOutcome.Succeeded, new FakeTimeProvider(s_start));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessBatchAsync(Owner, cancellation.Token));
    }

    [Fact]
    public async Task ProcessBatchAsync_DispatcherCancelled_DoesNotTreatItAsTransientFailureAsync()
    {
        LeasedWorkItem item = Item(WorkItemKind.Inbox, 1);
        var store = new RecordingWorkLeaseStore([item]);
        var dispatcher = new ScriptedDispatcher(_ => throw new OperationCanceledException());
        var processor = new WorkItemProcessor(
            store, dispatcher, new FixedRetryScheduler(3, TimeSpan.FromMinutes(1)), new FakeTimeProvider(s_start), s_options);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => processor.ProcessBatchAsync(Owner, CancellationToken.None));

        Assert.Empty(store.Outcomes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessBatchAsync_BlankLeaseOwner_ThrowsAsync(string owner)
    {
        WorkItemProcessor processor = CreateProcessor(
            new RecordingWorkLeaseStore([]), _ => DispatchOutcome.Succeeded, new FakeTimeProvider(s_start));

        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessBatchAsync(owner, CancellationToken.None));
    }

    [Fact]
    public void Constructor_NonPositiveLeaseDuration_Throws()
    {
        WorkerOptions invalid = s_options with { LeaseDuration = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkItemProcessor(
            new RecordingWorkLeaseStore([]),
            new ScriptedDispatcher(_ => DispatchOutcome.Succeeded),
            new FixedRetryScheduler(3, TimeSpan.FromMinutes(1)),
            new FakeTimeProvider(s_start),
            invalid));
    }

    private static LeasedWorkItem Item(WorkItemKind kind, int attemptNumber)
    {
        return new LeasedWorkItem(kind, Guid.NewGuid(), Guid.NewGuid(), "payload-1", attemptNumber);
    }

    private static WorkItemProcessor CreateProcessor(
        RecordingWorkLeaseStore store,
        Func<LeasedWorkItem, DispatchOutcome> script,
        FakeTimeProvider clock,
        int maxTotalAttempts = 3)
    {
        return new WorkItemProcessor(
            store,
            new ScriptedDispatcher(script),
            new FixedRetryScheduler(maxTotalAttempts, TimeSpan.FromMinutes(1)),
            clock,
            s_options);
    }
}
