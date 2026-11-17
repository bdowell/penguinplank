using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Time.Testing;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Integrations.Worker;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Auditing;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests for worker leasing and crash-safe reclaim against a real isolated SQL Server
/// database built from the actual migrations (requirement 3.15). Each test runs as the system
/// worker so the audit interceptor attributes every worker mutation (requirement A7 9.6). Time is
/// driven by a <see cref="FakeTimeProvider"/>.
/// </summary>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class EfWorkLeaseStoreTests
{
    private const string WorkerA = "worker-a";
    private const string WorkerB = "worker-b";
    private const int LargeBatch = 100;
    private const int MaxAttempts = 3;

    private static readonly DateTimeOffset s_start = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_leaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_lockWaitLimit = TimeSpan.FromSeconds(30);

    private readonly SqlServerDatabaseFixture _fixture;

    public EfWorkLeaseStoreTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LeaseNextAsync_ReadyInboxItem_LeasesAndRecordsFirstAttemptAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertInboxAsync(connectionId);

        IReadOnlyList<LeasedWorkItem> leased = await LeaseAsync(WorkerA, clock);

        LeasedWorkItem item = Assert.Single(leased);
        Assert.Equal(new LeasedWorkItem(WorkItemKind.Inbox, itemId, connectionId, "payload-1", 1), item);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationInbox row = await verify.IntegrationInbox.SingleAsync(r => r.Id == itemId);
        Assert.Equal(InboxStatus.Processing, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal(WorkerA, row.LeaseOwner);
        Assert.Equal(s_start + s_leaseDuration, row.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task LeaseNextAsync_ReadyOutboxItem_LeasesAndRecordsFirstAttemptAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertOutboxAsync(connectionId);

        IReadOnlyList<LeasedWorkItem> leased = await LeaseAsync(WorkerA, clock);

        Assert.Equal(WorkItemKind.Outbox, Assert.Single(leased).Kind);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationOutbox row = await verify.IntegrationOutbox.SingleAsync(r => r.Id == itemId);
        Assert.Equal(OutboxStatus.Dispatching, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal(WorkerA, row.LeaseOwner);
    }

    [Fact]
    public async Task LeaseNextAsync_UnexpiredLease_IsNotLeasedToAnotherWorkerAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        await InsertInboxAsync(connectionId);
        await LeaseAsync(WorkerA, clock);

        clock.Advance(s_leaseDuration - TimeSpan.FromSeconds(1));
        IReadOnlyList<LeasedWorkItem> second = await LeaseAsync(WorkerB, clock);

        Assert.Empty(second);
    }

    [Fact]
    public async Task LeaseNextAsync_ExpiredLease_IsReclaimedWithIncrementedAttemptAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertInboxAsync(connectionId);
        await LeaseAsync(WorkerA, clock);

        // Worker A crashed without reporting an outcome; its lease runs out.
        clock.Advance(s_leaseDuration);
        IReadOnlyList<LeasedWorkItem> reclaimed = await LeaseAsync(WorkerB, clock);

        LeasedWorkItem item = Assert.Single(reclaimed);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(2, item.AttemptNumber);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationInbox row = await verify.IntegrationInbox.SingleAsync(r => r.Id == itemId);
        Assert.Equal(WorkerB, row.LeaseOwner);
        Assert.Equal(2, row.Attempts);
        Assert.Equal(clock.GetUtcNow() + s_leaseDuration, row.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task CompleteAsync_StaleWorkerAfterReclaim_ReturnsFalseAndKeepsNewHolderAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertOutboxAsync(connectionId);
        LeasedWorkItem staleLease = Assert.Single(await LeaseAsync(WorkerA, clock));
        clock.Advance(s_leaseDuration);
        await LeaseAsync(WorkerB, clock);

        bool completedByStale = await ExecuteAsStoreAsync(
            store => store.CompleteAsync(staleLease, WorkerA, clock.GetUtcNow(), CancellationToken.None));

        Assert.False(completedByStale);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationOutbox row = await verify.IntegrationOutbox.SingleAsync(r => r.Id == itemId);
        Assert.Equal(OutboxStatus.Dispatching, row.Status);
        Assert.Equal(WorkerB, row.LeaseOwner);
    }

    [Fact]
    public async Task CompleteAsync_CurrentHolder_MarksProcessedAndClearsLeaseAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertInboxAsync(connectionId);
        LeasedWorkItem lease = Assert.Single(await LeaseAsync(WorkerA, clock));

        bool completed = await ExecuteAsStoreAsync(
            store => store.CompleteAsync(lease, WorkerA, clock.GetUtcNow(), CancellationToken.None));

        Assert.True(completed);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationInbox row = await verify.IntegrationInbox.SingleAsync(r => r.Id == itemId);
        Assert.Equal(InboxStatus.Processed, row.Status);
        Assert.Null(row.LeaseOwner);
        Assert.Null(row.LeaseExpiresAtUtc);
        Assert.Equal(1, row.Attempts);
        Assert.Empty(await LeaseAsync(WorkerB, clock));
    }

    [Fact]
    public async Task RescheduleAsync_RetryTimeNotReached_ItemIsNotLeasedUntilItArrivesAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertOutboxAsync(connectionId);
        LeasedWorkItem lease = Assert.Single(await LeaseAsync(WorkerA, clock));
        DateTimeOffset retryAt = clock.GetUtcNow() + TimeSpan.FromMinutes(2);
        Assert.True(await ExecuteAsStoreAsync(
            store => store.RescheduleAsync(lease, WorkerA, retryAt, clock.GetUtcNow(), CancellationToken.None)));

        clock.Advance(TimeSpan.FromMinutes(1));
        IReadOnlyList<LeasedWorkItem> early = await LeaseAsync(WorkerB, clock);
        clock.Advance(TimeSpan.FromMinutes(1));
        IReadOnlyList<LeasedWorkItem> due = await LeaseAsync(WorkerB, clock);

        Assert.Empty(early);
        LeasedWorkItem retried = Assert.Single(due);
        Assert.Equal(itemId, retried.Id);
        Assert.Equal(2, retried.AttemptNumber);
    }

    [Fact]
    public async Task ReleaseAsync_CurrentHolder_ReturnsItemToReadyWithoutCountingAnAttemptAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertInboxAsync(connectionId);
        LeasedWorkItem lease = Assert.Single(await LeaseAsync(WorkerA, clock));

        Assert.True(await ExecuteAsStoreAsync(
            store => store.ReleaseAsync(lease, WorkerA, clock.GetUtcNow(), CancellationToken.None)));
        LeasedWorkItem again = Assert.Single(await LeaseAsync(WorkerB, clock));

        Assert.Equal(itemId, again.Id);
        Assert.Equal(1, again.AttemptNumber);
    }

    [Fact]
    public async Task LeaseNextAsync_AttemptsExhausted_IsNeverLeasedAgainAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        await InsertInboxAsync(connectionId, attempts: MaxAttempts, status: InboxStatus.Failed);

        Assert.Empty(await LeaseAsync(WorkerA, clock));
    }

    [Fact]
    public async Task LeaseNextAsync_DisabledOrPausedConnection_IsNotLeasedAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid disabled = await ArrangeConnectionAsync(enabled: false);
        await InsertInboxAsync(disabled);
        Guid paused = await InsertConnectionAsync(enabled: true, isPaused: true);
        await InsertOutboxAsync(paused);

        Assert.Empty(await LeaseAsync(WorkerA, clock));
    }

    [Fact]
    public async Task LeaseNextAsync_BatchSizeSmallerThanReadyItems_LeasesOldestFirstAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid oldest = await InsertInboxAsync(connectionId, createdAt: s_start.AddMinutes(-3));
        Guid middle = await InsertInboxAsync(connectionId, createdAt: s_start.AddMinutes(-2));
        await InsertInboxAsync(connectionId, createdAt: s_start.AddMinutes(-1));

        IReadOnlyList<LeasedWorkItem> leased = await LeaseAsync(WorkerA, clock, batchSize: 2);

        Assert.Equal([oldest, middle], leased.Select(item => item.Id));
    }

    [Fact]
    public async Task LeaseNextAsync_RowLockedByAnotherTransaction_SkipsItAndLeasesTheRestAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        DateTimeOffset lockedCreatedAt = s_start.AddMinutes(-2);
        Guid locked = await InsertInboxAsync(connectionId, createdAt: lockedCreatedAt);
        Guid free = await InsertInboxAsync(connectionId, createdAt: s_start.AddMinutes(-1));

        // Lock the row the way a competing lease does: an update lock on its clustered-index key
        // (CreatedAtUtc). INDEX(1) forces the clustered index, so the lock does not depend on the
        // plan; a covering seek on the nonclustered Id key would lock only that key instead.
        await using PenguinPlankDbContext holder = _fixture.CreateContext();
        await using IDbContextTransaction transaction = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlAsync($"""
            SELECT Id FROM IntegrationInbox WITH (UPDLOCK, ROWLOCK, INDEX(1))
            WHERE CreatedAtUtc = {lockedCreatedAt} AND Id = {locked}
            """);

        // A lease that waited on the lock instead of skipping it would block until the rollback,
        // so a bounded wait turns that regression into a failure rather than a hung run.
        IReadOnlyList<LeasedWorkItem> leased = await LeaseAsync(WorkerB, clock).WaitAsync(s_lockWaitLimit);

        Assert.Equal(free, Assert.Single(leased).Id);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task LeaseNextAsync_SystemWorkerActor_AuditsTheLeaseMutationAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertInboxAsync(connectionId);

        await LeaseAsync(WorkerA, clock);

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        AuditEntry entry = await verify.AuditEntries.SingleAsync(e => e.EntityId == itemId);
        Assert.Equal(ActorContext.SystemWorkerId, entry.ActorId);
        Assert.Equal(nameof(IntegrationInbox) + AuditOperation.Updated, entry.Action);
        Assert.Equal(clock.GetUtcNow(), entry.Timestamp);
    }

    [Fact]
    public async Task ProcessBatchAsync_CrashThenReclaim_RecordsEveryAttemptAndAuditsAsSystemWorkerAsync()
    {
        var clock = new FakeTimeProvider(s_start);
        Guid connectionId = await ArrangeConnectionAsync();
        Guid itemId = await InsertOutboxAsync(connectionId);

        // Cycle 1: the dispatcher fails transiently, so the item is rescheduled one minute out.
        WorkBatchResult first = await RunCycleAsync(WorkerA, clock, DispatchOutcome.TransientFailure);
        // Cycle 2: the retry is due. The lease is taken, then the worker "crashes" (lease only).
        clock.Advance(TimeSpan.FromMinutes(1));
        LeasedWorkItem crashed = Assert.Single(await LeaseAsync(WorkerA, clock));
        // Cycle 3: after the lease expires another worker reclaims and finishes the item.
        clock.Advance(s_leaseDuration);
        WorkBatchResult third = await RunCycleAsync(WorkerB, clock, DispatchOutcome.Succeeded);

        Assert.Equal(new WorkBatchResult(1, 0, 1, 0), first);
        Assert.Equal(2, crashed.AttemptNumber);
        Assert.Equal(new WorkBatchResult(1, 1, 0, 0), third);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationOutbox row = await verify.IntegrationOutbox.SingleAsync(r => r.Id == itemId);
        Assert.Equal(OutboxStatus.Dispatched, row.Status);
        Assert.Equal(3, row.Attempts);
        List<AuditEntry> entries = await verify.AuditEntries.Where(e => e.EntityId == itemId).ToListAsync();
        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.Equal(ActorContext.SystemWorkerId, entry.ActorId));
    }

    private async Task<WorkBatchResult> RunCycleAsync(string owner, FakeTimeProvider clock, DispatchOutcome outcome)
    {
        await using PenguinPlankDbContext context = CreateWorkerContext(clock);
        var processor = new WorkItemProcessor(
            new EfWorkLeaseStore(context),
            new FixedOutcomeDispatcher(outcome),
            new FixedRetryScheduler(MaxAttempts, TimeSpan.FromMinutes(1)),
            clock,
            new WorkerOptions(TimeSpan.FromSeconds(30), s_leaseDuration, LargeBatch));

        return await processor.ProcessBatchAsync(owner, CancellationToken.None);
    }

    private async Task<IReadOnlyList<LeasedWorkItem>> LeaseAsync(
        string owner,
        FakeTimeProvider clock,
        int batchSize = LargeBatch)
    {
        return await ExecuteAsStoreAsync(
            store => store.LeaseNextAsync(
                owner,
                clock.GetUtcNow(),
                clock.GetUtcNow() + s_leaseDuration,
                batchSize,
                MaxAttempts,
                CancellationToken.None),
            clock);
    }

    private async Task<T> ExecuteAsStoreAsync<T>(Func<EfWorkLeaseStore, Task<T>> action, TimeProvider? clock = null)
    {
        await using PenguinPlankDbContext context = CreateWorkerContext(clock ?? new FakeTimeProvider(s_start));
        return await action(new EfWorkLeaseStore(context));
    }

    /// <summary>A context with the audit interceptor attached and the system worker as the actor.</summary>
    private PenguinPlankDbContext CreateWorkerContext(TimeProvider clock)
    {
        var accessor = new AmbientActorContextAccessor();
        accessor.SetCurrent(ActorContext.SystemWorker());

        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>()
                .UseSqlServer(_fixture.ConnectionString)
                .AddInterceptors(new AuditSaveChangesInterceptor(accessor, clock))
                .Options;

        return new PenguinPlankDbContext(options);
    }

    /// <summary>
    /// Makes this test's connection the only leasable source: the database is shared by the
    /// collection, so rows left by other tests are taken out of play first.
    /// </summary>
    private async Task<Guid> ArrangeConnectionAsync(bool enabled = true)
    {
        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlAsync($"UPDATE IntegrationConnections SET Enabled = 0");
        }

        return await InsertConnectionAsync(enabled, isPaused: false);
    }

    private async Task<Guid> InsertConnectionAsync(bool enabled, bool isPaused)
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
            IsPaused = isPaused,
        };
        context.IntegrationConnections.Add(connection);
        await context.SaveChangesAsync();
        return connection.Id;
    }

    private async Task<Guid> InsertInboxAsync(
        Guid connectionId,
        int attempts = 0,
        InboxStatus status = InboxStatus.Received,
        DateTimeOffset? createdAt = null)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var row = new IntegrationInbox
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            DeliveryKey = $"delivery-{Guid.NewGuid():N}",
            PayloadReference = "payload-1",
            SignatureState = "Valid",
            Status = status,
            Attempts = attempts,
            ReceivedAtUtc = createdAt ?? s_start.AddMinutes(-10),
            CreatedAtUtc = createdAt ?? s_start.AddMinutes(-10),
            UpdatedAtUtc = createdAt ?? s_start.AddMinutes(-10),
        };
        context.IntegrationInbox.Add(row);
        await context.SaveChangesAsync();
        return row.Id;
    }

    private async Task<Guid> InsertOutboxAsync(Guid connectionId)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var row = new IntegrationOutbox
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            OperationKey = $"op-{Guid.NewGuid():N}",
            PayloadReference = "payload-1",
            CreatedAtUtc = s_start.AddMinutes(-10),
            UpdatedAtUtc = s_start.AddMinutes(-10),
        };
        context.IntegrationOutbox.Add(row);
        await context.SaveChangesAsync();
        return row.Id;
    }

    private sealed class FixedOutcomeDispatcher : IWorkItemDispatcher
    {
        private readonly DispatchOutcome _outcome;

        public FixedOutcomeDispatcher(DispatchOutcome outcome)
        {
            _outcome = outcome;
        }

        public Task<DispatchOutcome> DispatchAsync(LeasedWorkItem item, CancellationToken cancellationToken)
        {
            return Task.FromResult(_outcome);
        }
    }

    private sealed class FixedRetryScheduler : IRetryScheduler
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
}
