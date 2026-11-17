using CsCheck;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Feature: penguin-plank-phase-a, Property 18: Inbox dedupe by delivery key is idempotent.
/// For any sequence of inbound deliveries containing repeated or out-of-order delivery keys, each
/// distinct key is durably recorded at most once and replaying a key produces no additional effect.
/// </summary>
/// <remarks>
/// Dedupe is decided by the unique index on <c>IntegrationInbox.DeliveryKey</c> in SQL Server, so
/// this property runs against the real isolated database rather than in the unit suite. Each
/// iteration replays a generated sequence of deliveries drawn from a small key space (so keys
/// repeat and arrive out of order) and checks that each distinct key is recorded exactly once, the
/// first delivery wins, and replays write nothing. Each iteration uses its own connection so
/// iterations cannot see each other's rows.
///
/// **Validates: Requirements 3.8**
/// </remarks>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class InboxDedupePropertyTests
{
    /// <summary>The design mandates at least 100 iterations per correctness property.</summary>
    private const int Iterations = 100;

    private const int KeySpace = 5;

    private static readonly Gen<int[]> s_genKeySequence = Gen.Int[0, KeySpace - 1].Array[1, 12];

    private readonly SqlServerDatabaseFixture _fixture;

    public InboxDedupePropertyTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TryRecordAsync_RepeatedAndOutOfOrderKeys_RecordsEachDistinctKeyOnceAsync()
    {
        await s_genKeySequence.SampleAsync(
            async sequence =>
            {
                Guid connectionId = await InsertConnectionAsync();
                string prefix = $"evt-{Guid.NewGuid():N}-";
                var firstPayloadByKey = new Dictionary<string, string>();

                for (int position = 0; position < sequence.Length; position++)
                {
                    string key = prefix + sequence[position];
                    string payload = $"payload-{position}";
                    bool expectedNew = firstPayloadByKey.TryAdd(key, payload);

                    bool recorded = await RecordAsync(connectionId, key, payload);

                    Assert.Equal(expectedNew, recorded);
                }

                await using PenguinPlankDbContext verify = _fixture.CreateContext();
                Dictionary<string, string> stored = await verify.IntegrationInbox
                    .Where(row => row.ConnectionId == connectionId)
                    .ToDictionaryAsync(row => row.DeliveryKey, row => row.PayloadReference);
                Assert.Equal(firstPayloadByKey.Count, stored.Count);
                foreach ((string key, string payload) in firstPayloadByKey)
                {
                    Assert.Equal(payload, stored[key]);
                }
            },
            iter: Iterations,
            threads: 1);
    }

    private async Task<bool> RecordAsync(Guid connectionId, string key, string payload)
    {
        var delivery = new InboundDelivery(connectionId, new DeliveryKey(key), new PayloadRef(payload), "valid");
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
