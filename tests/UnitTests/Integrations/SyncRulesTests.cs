using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;

namespace UnitTests.Integrations;

/// <summary>Unit tests for the sync cursor, pause, exception-queue, and redaction rules.</summary>
public class SyncRulesTests
{
    [Theory]
    [InlineData(null, null, CheckpointAdvanceOutcome.Advanced)]
    [InlineData("c1", "c1", CheckpointAdvanceOutcome.Advanced)]
    [InlineData("c2", "c1", CheckpointAdvanceOutcome.StaleCursor)]
    [InlineData("c1", null, CheckpointAdvanceOutcome.StaleCursor)]
    [InlineData(null, "c1", CheckpointAdvanceOutcome.StaleCursor)]
    public void Decide_StoredAndExpectedCursor_ReturnsOutcome(
        string? stored,
        string? expected,
        CheckpointAdvanceOutcome outcome)
    {
        Assert.Equal(outcome, CursorAdvancePolicy.Decide(stored, expected));
    }

    private static readonly double[] s_expected = new[] { 2.0, 2.0, 4.0, 8.0, 10.0, 10.0 };

    [Fact]
    public void Schedule_ConfiguredOptions_IsBoundedCappedAndRespectsRateFloor()
    {
        var options = new RetryBackoffOptions(
            MaxAttempts: 6,
            InitialDelay: TimeSpan.FromSeconds(1),
            MaxDelay: TimeSpan.FromSeconds(10),
            JitterRatio: 0.5,
            MinimumInterval: TimeSpan.FromSeconds(2));

        var schedule = RetryBackoffPolicy.Schedule(options, _ => 1.0);

        Assert.Equal(6, schedule.Count);
        Assert.Equal(
            s_expected,
            schedule.Select(a => a.BaseDelay.TotalSeconds));
        Assert.All(schedule, a => Assert.Equal(a.BaseDelay.TotalSeconds * 1.5, a.Delay.TotalSeconds, 6));
    }

    [Fact]
    public void Schedule_ZeroAttempts_ReturnsEmpty()
    {
        var options = new RetryBackoffOptions(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 0.2, TimeSpan.Zero);

        Assert.Empty(RetryBackoffPolicy.Schedule(options, _ => 0.5));
    }

    [Fact]
    public void Schedule_InvalidJitterRatio_Throws()
    {
        var options = new RetryBackoffOptions(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 1.5, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => RetryBackoffPolicy.Schedule(options, _ => 0.5));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void CanStartOutgoingWork_EnabledAndPaused_OnlyEnabledUnpausedMayStart(
        bool enabled,
        bool isPaused,
        bool expected)
    {
        Assert.Equal(expected, OutgoingWorkPolicy.CanStartOutgoingWork(enabled, isPaused));
    }

    [Fact]
    public void Apply_OpenRetry_MovesToRetryRequested()
    {
        Assert.Equal(
            SyncExceptionStatus.RetryRequested,
            SyncExceptionActions.Apply(SyncExceptionStatus.Open, SyncExceptionAction.Retry));
    }

    [Fact]
    public void Apply_RetryRequestedReconcile_MovesToResolved()
    {
        Assert.Equal(
            SyncExceptionStatus.Resolved,
            SyncExceptionActions.Apply(SyncExceptionStatus.RetryRequested, SyncExceptionAction.Reconcile));
    }

    [Theory]
    [InlineData(SyncExceptionStatus.Resolved, SyncExceptionAction.Retry)]
    [InlineData(SyncExceptionStatus.Resolved, SyncExceptionAction.Reconcile)]
    [InlineData(SyncExceptionStatus.RetryRequested, SyncExceptionAction.Retry)]
    public void Apply_UnavailableAction_ReturnsNull(SyncExceptionStatus status, SyncExceptionAction action)
    {
        Assert.Null(SyncExceptionActions.Apply(status, action));
    }

    [Fact]
    public void Available_ResolvedException_HasNoActions()
    {
        Assert.Empty(SyncExceptionActions.Available(SyncExceptionStatus.Resolved));
    }

    [Theory]
    [InlineData("Bearer abc123def failed")]
    [InlineData("request token=s3cr3tvalue rejected")]
    [InlineData("owner jane@example.com lacks permission")]
    [InlineData("key AbCdEfGhIjKlMnOpQrStUvWxYz0123456789 invalid")]
    public void Redact_SecretOrPersonalData_IsRemoved(string raw)
    {
        string redacted = SyncErrorRedactor.Redact(raw);

        Assert.Contains(SyncErrorRedactor.RedactionMarker, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123def", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3tvalue", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("jane@example.com", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("AbCdEfGhIjKlMnOpQrStUvWxYz0123456789", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_BlankOrOverlong_ReturnsBoundedNonEmptyText()
    {
        Assert.Equal(SyncErrorRedactor.UnknownError, SyncErrorRedactor.Redact("  "));
        string overlong = string.Concat(Enumerable.Repeat("ab ", 2000));

        Assert.Equal(SyncErrorRedactor.MaximumLength, SyncErrorRedactor.Redact(overlong).Length);
    }

    [Fact]
    public async Task ApplyActionAsync_OpenRetry_StoresRetryRequestedAsync()
    {
        Guid id = Guid.NewGuid();
        var store = new FakeHistoryStore(Record(id, SyncExceptionStatus.Open));
        var service = new SyncHistoryService(store, TimeProvider.System);

        Result result = await service.ApplyActionAsync(id, SyncExceptionAction.Retry, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncExceptionStatus.RetryRequested, store.Stored!.Status);
    }

    [Fact]
    public async Task ApplyActionAsync_UnavailableAction_FailsWithoutChangeAsync()
    {
        Guid id = Guid.NewGuid();
        var store = new FakeHistoryStore(Record(id, SyncExceptionStatus.Resolved));
        var service = new SyncHistoryService(store, TimeProvider.System);

        Result result = await service.ApplyActionAsync(id, SyncExceptionAction.Retry, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SyncExceptionStatus.Resolved, store.Stored!.Status);
    }

    [Fact]
    public async Task ApplyActionAsync_MissingException_FailsAsync()
    {
        var service = new SyncHistoryService(new FakeHistoryStore(null), TimeProvider.System);

        Result result = await service.ApplyActionAsync(Guid.NewGuid(), SyncExceptionAction.Retry, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetViewAsync_UnresolvedException_ListsAvailableActionsAsync()
    {
        Guid id = Guid.NewGuid();
        var store = new FakeHistoryStore(Record(id, SyncExceptionStatus.Open));
        var service = new SyncHistoryService(store, TimeProvider.System);

        SyncHistoryView? view = await service.GetViewAsync(store.ConnectionId, CancellationToken.None);

        Assert.NotNull(view);
        SyncExceptionView item = Assert.Single(view.Exceptions);
        Assert.Equal([SyncExceptionAction.Retry, SyncExceptionAction.Reconcile], item.AvailableActions);
    }

    private static SyncExceptionRecord Record(Guid id, SyncExceptionStatus status)
    {
        return new SyncExceptionRecord(id, Guid.NewGuid(), "order:1", "denied", status, DateTimeOffset.UnixEpoch);
    }

    private sealed class FakeHistoryStore : ISyncHistoryStore
    {
        public FakeHistoryStore(SyncExceptionRecord? stored)
        {
            Stored = stored;
        }

        public Guid ConnectionId { get; } = Guid.NewGuid();

        public SyncExceptionRecord? Stored { get; private set; }

        public Task BeginRunAsync(NewSyncRun run, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> CompleteRunAsync(SyncRunCompletion completion, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> QueueExceptionAsync(SyncExceptionDraft exception, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<SyncExceptionRecord?> GetExceptionAsync(Guid exceptionId, CancellationToken cancellationToken) =>
            Task.FromResult(Stored is not null && Stored.Id == exceptionId ? Stored : null);

        public Task<bool> SetExceptionStatusAsync(
            Guid exceptionId,
            SyncExceptionStatus status,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            Stored = Stored! with { Status = status };
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<SyncExceptionRecord>> ListUnresolvedExceptionsAsync(
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<SyncExceptionRecord> list = Stored is null ? [] : [Stored];
            return Task.FromResult(list);
        }

        public Task<SyncHistorySummary?> GetSummaryAsync(Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<SyncHistorySummary?>(new SyncHistorySummary(connectionId, false, null, 0, 0));
    }
}
