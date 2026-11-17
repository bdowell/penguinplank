using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests for the transactional outbox store against a real isolated SQL Server
/// database (requirement 3.14). Broader atomicity coverage belongs to tasks 11.8 and 11.10.
/// </summary>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class OutboxStoreTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public OutboxStoreTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EnqueueAsync_WithBusinessChange_PersistsBothInSingleSaveAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = $"op-{Guid.NewGuid():N}";

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            IntegrationConnection connection = await context.IntegrationConnections.SingleAsync(row => row.Id == connectionId);
            connection.Enabled = true;

            OutboxEnqueueOutcome outcome = await new EfOutboxStore(context, TimeProvider.System)
                .EnqueueAsync(Job(connectionId, key), CancellationToken.None);

            Assert.Equal(OutboxEnqueueOutcome.Staged, outcome);
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.True((await verify.IntegrationConnections.SingleAsync(row => row.Id == connectionId)).Enabled);
        IntegrationOutbox job = await verify.IntegrationOutbox.SingleAsync(row => row.OperationKey == key);
        Assert.Equal(OutboxStatus.Pending, job.Status);
        Assert.Equal(connectionId, job.ConnectionId);
    }

    [Fact]
    public async Task EnqueueAsync_DuplicateOperationKey_ReturnsAlreadyEnqueuedAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = $"op-{Guid.NewGuid():N}";

        OutboxEnqueueOutcome first = await EnqueueAndSaveAsync(Job(connectionId, key));
        OutboxEnqueueOutcome second = await EnqueueAndSaveAsync(Job(connectionId, key));

        Assert.Equal(OutboxEnqueueOutcome.Staged, first);
        Assert.Equal(OutboxEnqueueOutcome.AlreadyEnqueued, second);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.IntegrationOutbox.CountAsync(row => row.OperationKey == key));
    }

    [Fact]
    public async Task EnqueueAsync_FailedCommit_PersistsNeitherChangeNorJobAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = $"op-{Guid.NewGuid():N}";

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            IntegrationConnection connection = await context.IntegrationConnections.SingleAsync(row => row.Id == connectionId);
            connection.Enabled = true;

            OutboxEnqueueOutcome outcome = await new EfOutboxStore(context, TimeProvider.System)
                .EnqueueAsync(Job(connectionId, key), CancellationToken.None);
            Assert.Equal(OutboxEnqueueOutcome.Staged, outcome);

            // A concurrent writer commits the same key after the pre-check, so this commit must fail.
            await EnqueueAndSaveAsync(Job(connectionId, key));

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.False((await verify.IntegrationConnections.SingleAsync(row => row.Id == connectionId)).Enabled);
        Assert.Equal(1, await verify.IntegrationOutbox.CountAsync(row => row.OperationKey == key));
    }

    private static OutboxJob Job(Guid connectionId, string key)
    {
        return new OutboxJob(connectionId, new OperationKey(key), new PayloadRef("payload-1"));
    }

    private async Task<OutboxEnqueueOutcome> EnqueueAndSaveAsync(OutboxJob job)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        OutboxEnqueueOutcome outcome = await new EfOutboxStore(context, TimeProvider.System)
            .EnqueueAsync(job, CancellationToken.None);
        await context.SaveChangesAsync();
        return outcome;
    }

    private async Task<Guid> InsertConnectionAsync()
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
            Enabled = false,
        };
        context.IntegrationConnections.Add(connection);
        await context.SaveChangesAsync();
        return connection.Id;
    }
}
