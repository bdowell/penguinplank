using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests for the durable inbox store against a real isolated SQL Server database
/// (requirements 3.6, 3.8).
/// </summary>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class InboxStoreTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public InboxStoreTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TryRecordAsync_NewKey_PersistsReceivedItemAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = NewKey();

        bool recorded = await RecordAsync(Delivery(connectionId, key));

        Assert.True(recorded);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationInbox item = await verify.IntegrationInbox.SingleAsync(row => row.DeliveryKey == key);
        Assert.Equal(connectionId, item.ConnectionId);
        Assert.Equal(InboxStatus.Received, item.Status);
        Assert.Equal("payload-1", item.PayloadReference);
        Assert.Equal(0, item.Attempts);
    }

    [Fact]
    public async Task TryRecordAsync_RepeatedKey_ReturnsFalseAndWritesNothingAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = NewKey();

        bool first = await RecordAsync(Delivery(connectionId, key));
        bool second = await RecordAsync(Delivery(connectionId, key, payload: "payload-2"));

        Assert.True(first);
        Assert.False(second);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        IntegrationInbox item = await verify.IntegrationInbox.SingleAsync(row => row.DeliveryKey == key);
        Assert.Equal("payload-1", item.PayloadReference);
    }

    [Fact]
    public async Task TryRecordAsync_ConcurrentSameKey_ExactlyOneRecordsAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        string key = NewKey();

        Task<bool>[] attempts = Enumerable.Range(0, 6)
            .Select(_ => RecordAsync(Delivery(connectionId, key)))
            .ToArray();
        bool[] results = await Task.WhenAll(attempts);

        Assert.Single(results, recorded => recorded);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.IntegrationInbox.CountAsync(row => row.DeliveryKey == key));
    }

    [Fact]
    public async Task TryRecordAsync_OverlongSignatureState_ThrowsArgumentExceptionAsync()
    {
        Guid connectionId = await InsertConnectionAsync();
        InboundDelivery delivery = Delivery(connectionId, NewKey()) with { SignatureState = new string('s', 65) };

        await Assert.ThrowsAsync<ArgumentException>(() => RecordAsync(delivery));
    }

    [Fact]
    public async Task TryRecordAsync_UnknownConnection_ThrowsAndDoesNotReportRecordedAsync()
    {
        InboundDelivery delivery = Delivery(Guid.NewGuid(), NewKey());

        await Assert.ThrowsAsync<DbUpdateException>(() => RecordAsync(delivery));
    }

    private static string NewKey() => $"evt-{Guid.NewGuid():N}";

    private static InboundDelivery Delivery(Guid connectionId, string key, string payload = "payload-1")
    {
        return new InboundDelivery(connectionId, new DeliveryKey(key), new PayloadRef(payload), "valid");
    }

    private async Task<bool> RecordAsync(InboundDelivery delivery)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        return await new EfInboxStore(context, TimeProvider.System)
            .TryRecordAsync(delivery, CancellationToken.None);
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
