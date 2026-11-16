using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests for the sync checkpoint, history/exception, and pause stores against a real
/// isolated SQL Server database (requirements 3.9, 3.11, 3.12, 3.13).
/// </summary>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class SyncStoreTests
{
    private const string Resource = "orders";

    private readonly SqlServerDatabaseFixture _fixture;

    public SyncStoreTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdvanceAsync_FirstThenMatchingThenStale_AdvancesOnlyWhenExpectedMatchesAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CheckpointAdvanceOutcome first = await AdvanceAsync(connectionId, null, "c1", now);
        CheckpointAdvanceOutcome second = await AdvanceAsync(connectionId, "c1", "c2", now);
        CheckpointAdvanceOutcome stale = await AdvanceAsync(connectionId, "c1", "c3", now);

        Assert.Equal(CheckpointAdvanceOutcome.Advanced, first);
        Assert.Equal(CheckpointAdvanceOutcome.Advanced, second);
        Assert.Equal(CheckpointAdvanceOutcome.StaleCursor, stale);

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        SyncCheckpointState? state = await new EfSyncCheckpointStore(verify).GetAsync(connectionId, Resource, CancellationToken.None);
        Assert.Equal("c2", state?.Cursor);
    }

    [Fact]
    public async Task AdvanceAsync_ConcurrentFirstAdvance_ExactlyOneAdvancesAsync()
    {
        Guid connectionId = await InsertConnectionAsync();

        Task<CheckpointAdvanceOutcome>[] attempts = Enumerable.Range(0, 4)
            .Select(index => AdvanceAsync(connectionId, null, $"c{index}", DateTimeOffset.UtcNow))
            .ToArray();
        CheckpointAdvanceOutcome[] outcomes = await Task.WhenAll(attempts);

        Assert.Single(outcomes, outcome => outcome == CheckpointAdvanceOutcome.Advanced);
    }

    [Fact]
    public async Task MarkReconciledAsync_NoCheckpoint_CreatesItWithoutCursorAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        DateTimeOffset at = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            await new EfSyncCheckpointStore(context).MarkReconciledAsync(connectionId, Resource, at, CancellationToken.None);
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        SyncCheckpointState? state = await new EfSyncCheckpointStore(verify).GetAsync(connectionId, Resource, CancellationToken.None);
        Assert.NotNull(state);
        Assert.Null(state.Cursor);
        Assert.Equal(at, state.LastReconciledAtUtc);
    }

    [Fact]
    public async Task QueueExceptionAsync_UnknownRun_ReturnsFalseAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var draft = new SyncExceptionDraft(Guid.NewGuid(), Guid.NewGuid(), "order:1", "denied", DateTimeOffset.UtcNow);

        Assert.False(await new EfSyncHistoryStore(context).QueueExceptionAsync(draft, CancellationToken.None));
    }

    [Fact]
    public async Task GetSummaryAsync_RunExceptionAndOutboxJob_ReportsLastSuccessBacklogAndFailedAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        Guid runId = Guid.NewGuid();
        DateTimeOffset success = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            var store = new EfSyncHistoryStore(context);
            await store.BeginRunAsync(new NewSyncRun(runId, connectionId, Resource, success.AddMinutes(-1)), CancellationToken.None);
            await store.CompleteRunAsync(new SyncRunCompletion(runId, 5, 1, success, success), CancellationToken.None);
            await store.QueueExceptionAsync(
                new SyncExceptionDraft(Guid.NewGuid(), runId, "order:9", "permission denied", success),
                CancellationToken.None);
            context.IntegrationOutbox.Add(new IntegrationOutbox
            {
                Id = Guid.NewGuid(),
                ConnectionId = connectionId,
                OperationKey = $"op-{Guid.NewGuid():N}",
                PayloadReference = "payload",
                Status = OutboxStatus.Pending,
            });
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        SyncHistorySummary? summary = await new EfSyncHistoryStore(verify).GetSummaryAsync(connectionId, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(success, summary.LastSuccessAtUtc);
        Assert.Equal(1, summary.BacklogCount);
        Assert.Equal(1, summary.FailedItemCount);
        Assert.False(summary.IsPaused);
    }

    [Fact]
    public async Task ApplyActionAsync_ReconcileException_RemovesItFromUnresolvedListAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        Guid runId = Guid.NewGuid();
        Guid exceptionId = Guid.NewGuid();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            var store = new EfSyncHistoryStore(context);
            await store.BeginRunAsync(new NewSyncRun(runId, connectionId, Resource, DateTimeOffset.UtcNow), CancellationToken.None);
            await store.QueueExceptionAsync(
                new SyncExceptionDraft(exceptionId, runId, "order:2", "denied", DateTimeOffset.UtcNow),
                CancellationToken.None);
        }

        await using PenguinPlankDbContext actContext = _fixture.CreateContext();
        var service = new SyncHistoryService(new EfSyncHistoryStore(actContext), TimeProvider.System);
        var result = await service.ApplyActionAsync(exceptionId, SyncExceptionAction.Reconcile, CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Empty(await new EfSyncHistoryStore(verify).ListUnresolvedExceptionsAsync(connectionId, CancellationToken.None));
    }

    [Fact]
    public async Task PauseAsync_ThenResume_StopsOutgoingWorkAndPreservesHistoryAsync()
    {
        Guid connectionId = await InsertConnectionAsync(enabled: true);
        Guid runId = Guid.NewGuid();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            await new EfSyncHistoryStore(context)
                .BeginRunAsync(new NewSyncRun(runId, connectionId, Resource, DateTimeOffset.UtcNow), CancellationToken.None);
            await new EfSyncCheckpointStore(context)
                .AdvanceAsync(connectionId, Resource, null, "c1", DateTimeOffset.UtcNow, CancellationToken.None);
        }

        await using (PenguinPlankDbContext pauseContext = _fixture.CreateContext())
        {
            Assert.True(await new EfConnectionPauseStore(pauseContext, TimeProvider.System).PauseAsync(connectionId, CancellationToken.None));
        }

        await using (PenguinPlankDbContext pausedContext = _fixture.CreateContext())
        {
            OutgoingWorkState? paused = await new EfConnectionPauseStore(pausedContext, TimeProvider.System)
                .GetOutgoingWorkStateAsync(connectionId, CancellationToken.None);

            Assert.NotNull(paused);
            Assert.False(OutgoingWorkPolicy.CanStartOutgoingWork(paused.Enabled, paused.IsPaused));
            Assert.True(await pausedContext.SyncRuns.AnyAsync(run => run.Id == runId));
            Assert.Equal("c1", (await new EfSyncCheckpointStore(pausedContext).GetAsync(connectionId, Resource, CancellationToken.None))?.Cursor);
        }

        await using PenguinPlankDbContext resumeContext = _fixture.CreateContext();
        var pauseStore = new EfConnectionPauseStore(resumeContext, TimeProvider.System);
        Assert.True(await pauseStore.ResumeAsync(connectionId, CancellationToken.None));

        OutgoingWorkState? resumed = await pauseStore.GetOutgoingWorkStateAsync(connectionId, CancellationToken.None);
        Assert.NotNull(resumed);
        Assert.True(OutgoingWorkPolicy.CanStartOutgoingWork(resumed.Enabled, resumed.IsPaused));
    }

    [Fact]
    public async Task PauseAsync_UnknownConnection_ReturnsFalseAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();

        Assert.False(await new EfConnectionPauseStore(context, TimeProvider.System).PauseAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private async Task<CheckpointAdvanceOutcome> AdvanceAsync(
        Guid connectionId,
        string? expected,
        string next,
        DateTimeOffset now)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        return await new EfSyncCheckpointStore(context)
            .AdvanceAsync(connectionId, Resource, expected, next, now, CancellationToken.None);
    }

    private async Task<Guid> InsertConnectionAsync(bool enabled = false)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var connection = new IntegrationConnection
        {
            Id = Guid.NewGuid(),
            Platform = "Shopify",
            Account = $"shop-{Guid.NewGuid():N}",
            Scopes = "read_orders",
            ApiVersion = "2025-01",
            EnabledCapabilities = string.Empty,
            SyncDirection = SyncDirection.None,
            Health = ConnectionHealth.Unknown,
            Enabled = enabled,
        };
        context.IntegrationConnections.Add(connection);
        await context.SaveChangesAsync();
        return connection.Id;
    }
}
