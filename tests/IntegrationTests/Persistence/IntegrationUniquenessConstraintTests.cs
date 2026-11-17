using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Schema-level integration tests for the inbox, outbox and mapping unique indexes, run against a
/// real isolated SQL Server database built from the actual migrations (requirements 3.4, 3.8,
/// 3.14). Rows are inserted directly so the database constraint itself is proven, independent of
/// any application-level pre-check in the stores.
/// </summary>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class IntegrationUniquenessConstraintTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public IntegrationUniquenessConstraintTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateDeliveryKeySameConnection_ViolatesUniqueIndexAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = NewKey("evt");
        await InsertInboxAsync(connectionId, key);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => InsertInboxAsync(connectionId, key));

        Assert.True(IsUniqueConstraintViolation(exception));
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.IntegrationInbox.CountAsync(row => row.DeliveryKey == key));
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateDeliveryKeyDifferentConnection_ViolatesUniqueIndexAsync()
    {
        Guid firstConnectionId = await InsertConnectionAsync();
        Guid secondConnectionId = await InsertConnectionAsync();
        string key = NewKey("evt");
        await InsertInboxAsync(firstConnectionId, key);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => InsertInboxAsync(secondConnectionId, key));

        Assert.True(IsUniqueConstraintViolation(exception));
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateOperationKey_ViolatesUniqueIndexAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = NewKey("op");
        await InsertOutboxAsync(connectionId, key);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => InsertOutboxAsync(connectionId, key));

        Assert.True(IsUniqueConstraintViolation(exception));
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.IntegrationOutbox.CountAsync(row => row.OperationKey == key));
    }

    [Fact]
    public async Task SaveChangesAsync_OutboxInRolledBackTransaction_LeavesNoJobAndKeyIsReusableAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = NewKey("op");

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            IntegrationConnection connection =
                await context.IntegrationConnections.SingleAsync(row => row.Id == connectionId);
            connection.Enabled = true;
            context.IntegrationOutbox.Add(NewOutbox(connectionId, key));
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using (PenguinPlankDbContext verify = _fixture.CreateContext())
        {
            Assert.False((await verify.IntegrationConnections.SingleAsync(row => row.Id == connectionId)).Enabled);
            Assert.False(await verify.IntegrationOutbox.AnyAsync(row => row.OperationKey == key));
        }

        // The rolled-back key must not remain reserved: a later committed enqueue succeeds.
        await InsertOutboxAsync(connectionId, key);
        await using PenguinPlankDbContext final = _fixture.CreateContext();
        Assert.Equal(1, await final.IntegrationOutbox.CountAsync(row => row.OperationKey == key));
    }

    [Fact]
    public async Task SaveChangesAsync_OutboxInsertFailsMidTransaction_RollsBackBusinessChangeAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = NewKey("op");
        await InsertOutboxAsync(connectionId, key);

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            IntegrationConnection connection =
                await context.IntegrationConnections.SingleAsync(row => row.Id == connectionId);
            connection.Enabled = true;
            context.IntegrationOutbox.Add(NewOutbox(connectionId, key));

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            await transaction.RollbackAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.False((await verify.IntegrationConnections.SingleAsync(row => row.Id == connectionId)).Enabled);
        Assert.Equal(1, await verify.IntegrationOutbox.CountAsync(row => row.OperationKey == key));
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateMappingTriple_ViolatesUniqueIndexAsync()
    {
        (Guid connectionId, string account) = await InsertConnectionWithAccountAsync();
        await InsertMappingAsync(connectionId, account, "gid://dup");

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => InsertMappingAsync(connectionId, account, "gid://dup"));

        Assert.True(IsUniqueConstraintViolation(exception));
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.EntityMappings.CountAsync(
            row => row.Account == account && row.ExternalId == "gid://dup"));
    }

    [Fact]
    public async Task SaveChangesAsync_MappingTripleDiffersInAnyPart_IsAllowedAsync()
    {
        (Guid connectionId, string account) = await InsertConnectionWithAccountAsync();
        (Guid otherConnectionId, string otherAccount) = await InsertConnectionWithAccountAsync();
        await InsertMappingAsync(connectionId, account, "gid://base");

        await InsertMappingAsync(connectionId, account, "gid://other-id");
        await InsertMappingAsync(otherConnectionId, otherAccount, "gid://base");
        await InsertMappingAsync(connectionId, account, "gid://base", platform: "Etsy");

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(3, await verify.EntityMappings.CountAsync(row => row.Account == account));
        Assert.Equal(1, await verify.EntityMappings.CountAsync(row => row.Account == otherAccount));
    }

    private static string NewKey(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    // SQL Server error 2601 = duplicate key row; 2627 = unique constraint/PK violation.
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627);

    private static IntegrationOutbox NewOutbox(Guid connectionId, string operationKey) => new()
    {
        Id = Guid.NewGuid(),
        ConnectionId = connectionId,
        OperationKey = operationKey,
        PayloadReference = "payload-ref",
        Status = OutboxStatus.Pending,
    };

    private async Task InsertInboxAsync(Guid connectionId, string deliveryKey)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        context.IntegrationInbox.Add(new IntegrationInbox
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            DeliveryKey = deliveryKey,
            PayloadReference = "payload-ref",
            SignatureState = "valid",
            Status = InboxStatus.Received,
            ReceivedAtUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
    }

    private async Task InsertOutboxAsync(Guid connectionId, string operationKey)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        context.IntegrationOutbox.Add(NewOutbox(connectionId, operationKey));
        await context.SaveChangesAsync();
    }

    private async Task InsertMappingAsync(
        Guid connectionId,
        string account,
        string externalId,
        string platform = "Shopify")
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        context.EntityMappings.Add(new EntityMapping
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            Platform = platform,
            Account = account,
            ExternalId = externalId,
            OwnerType = "ProductVariant",
            InternalId = Guid.NewGuid(),
        });
        await context.SaveChangesAsync();
    }

    private async Task<Guid> InsertConnectionAsync()
    {
        (Guid connectionId, _) = await InsertConnectionWithAccountAsync();
        return connectionId;
    }

    private async Task<(Guid ConnectionId, string Account)> InsertConnectionWithAccountAsync()
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
        return (connection.Id, connection.Account);
    }
}
