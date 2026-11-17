using CsCheck;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Feature: penguin-plank-phase-a, Property 19: Transactional outbox atomicity.
/// For any business change that requires an outgoing integration job, the outbox job exists if and
/// only if the business change's transaction committed: a rolled-back change leaves no job and a
/// committed change always has its job.
/// </summary>
/// <remarks>
/// Atomicity is decided by SQL Server transactions, so the property runs against the real isolated
/// database. Each iteration applies a generated sequence of business changes (enable a connection
/// and enqueue its job), each ending in a commit, an explicit transaction rollback after the
/// writes were sent, or a context discarded without saving, then checks that the change and its
/// job are both present or both absent.
///
/// **Validates: Requirements 3.14**
/// </remarks>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class OutboxAtomicityPropertyTests
{
    /// <summary>The design mandates at least 100 iterations per correctness property.</summary>
    private const int Iterations = 100;

    private static readonly Gen<TransactionEnding[]> s_genEndings =
        Gen.Int[0, 2].Select(value => (TransactionEnding)value).Array[1, 8];

    private readonly SqlServerDatabaseFixture _fixture;

    public OutboxAtomicityPropertyTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private enum TransactionEnding
    {
        Commit,
        RollbackAfterSave,
        DiscardWithoutSave,
    }

    [Fact]
    public async Task EnqueueAsync_AnyTransactionEnding_JobExistsIffBusinessChangeCommittedAsync()
    {
        await s_genEndings.SampleAsync(
            async endings =>
            {
                for (int position = 0; position < endings.Length; position++)
                {
                    Guid connectionId = await InsertConnectionAsync();
                    string key = $"op-{Guid.NewGuid():N}";

                    await ApplyBusinessChangeAsync(connectionId, key, endings[position]);

                    bool expectCommitted = endings[position] == TransactionEnding.Commit;
                    await using PenguinPlankDbContext verify = _fixture.CreateContext();
                    bool enabled = (await verify.IntegrationConnections.SingleAsync(row => row.Id == connectionId)).Enabled;
                    int jobCount = await verify.IntegrationOutbox.CountAsync(row => row.OperationKey == key);

                    Assert.Equal(expectCommitted, enabled);
                    Assert.Equal(expectCommitted ? 1 : 0, jobCount);
                }
            },
            iter: Iterations,
            threads: 1);
    }

    private async Task ApplyBusinessChangeAsync(Guid connectionId, string key, TransactionEnding ending)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        IntegrationConnection connection = await context.IntegrationConnections.SingleAsync(row => row.Id == connectionId);
        connection.Enabled = true;
        var job = new OutboxJob(connectionId, new OperationKey(key), new PayloadRef("payload-1"));
        OutboxEnqueueOutcome outcome = await new EfOutboxStore(context, TimeProvider.System)
            .EnqueueAsync(job, CancellationToken.None);
        Assert.Equal(OutboxEnqueueOutcome.Staged, outcome);

        if (ending == TransactionEnding.DiscardWithoutSave)
        {
            await transaction.RollbackAsync();
            return;
        }

        await context.SaveChangesAsync();
        if (ending == TransactionEnding.Commit)
        {
            await transaction.CommitAsync();
        }
        else
        {
            await transaction.RollbackAsync();
        }
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
