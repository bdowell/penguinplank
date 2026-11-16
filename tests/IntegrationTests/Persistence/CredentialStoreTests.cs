using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Connection recording and credential-reference indirection against a real migrated SQL Server
/// database (requirement R11 / 3.1, 3.2, 3.3).
/// </summary>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class CredentialStoreTests
{
    private const string Locator = "keyvault://penguinplank/shopify-main";

    private static readonly DateTimeOffset s_start = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerDatabaseFixture _fixture;

    public CredentialStoreTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordAsync_NewConnection_PersistsDetailsDisabledWithoutCredentialAsync()
    {
        Guid id = Guid.NewGuid();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            var store = new EfIntegrationConnectionStore(context, new FixedTimeProvider(s_start));
            await store.RecordAsync(id, NewRegistration(), CancellationToken.None);
        }

        await using PenguinPlankDbContext readContext = _fixture.CreateContext();
        var readStore = new EfIntegrationConnectionStore(readContext, new FixedTimeProvider(s_start));
        IntegrationConnectionSummary? summary = await readStore.GetAsync(id, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal("Shopify", summary.Platform);
        Assert.Equal("penguin-shop", summary.Account);
        Assert.Equal("read_orders", summary.Scopes);
        Assert.Equal("2026-07", summary.ApiVersion);
        Assert.Equal("orders", summary.EnabledCapabilities);
        Assert.Equal(SyncDirection.Inbound, summary.SyncDirection);
        Assert.Equal(ConnectionHealth.Unknown, summary.Health);
        Assert.False(summary.Enabled);
        Assert.False(summary.HasCredential);
    }

    [Fact]
    public async Task ResolveAsync_StashedReference_ReturnsReferenceAndStoresOnlyLocatorAsync()
    {
        Guid id = await RecordConnectionAsync();

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            var store = new EfCredentialStore(context, new FixedTimeProvider(s_start));
            Result stashed = await store.StashReferenceAsync(id, NewReference(), CancellationToken.None);
            Assert.True(stashed.IsSuccess);
        }

        await using PenguinPlankDbContext readContext = _fixture.CreateContext();
        var readStore = new EfCredentialStore(readContext, new FixedTimeProvider(s_start));
        Result<CredentialResolution> resolved = await readStore.ResolveAsync(id, CancellationToken.None);

        Assert.True(resolved.IsSuccess);
        Assert.Equal(CredentialUseDecision.Usable, resolved.Value.Decision);
        Assert.Equal(Locator, resolved.Value.Reference?.Value);

        IntegrationConnection row = await readContext.IntegrationConnections
            .AsNoTracking()
            .SingleAsync(connection => connection.Id == id);
        Assert.Equal(Locator, row.CredentialReference);
    }

    [Theory]
    [InlineData(CredentialState.Revoked, CredentialUseDecision.Revoked)]
    [InlineData(CredentialState.Expired, CredentialUseDecision.Expired)]
    public async Task ResolveAsync_RevokedOrExpired_WithholdsReferenceAndRecordsStateAsync(
        CredentialState state,
        CredentialUseDecision expected)
    {
        Guid id = await RecordConnectionAsync();
        DateTimeOffset revokedAt = s_start.AddHours(3);

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            var store = new EfCredentialStore(context, new FixedTimeProvider(s_start));
            await store.StashReferenceAsync(id, NewReference(), CancellationToken.None);
        }

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            var store = new EfCredentialStore(context, new FixedTimeProvider(revokedAt));
            Result recorded = await store.RecordCredentialStateAsync(id, state, CancellationToken.None);
            Assert.True(recorded.IsSuccess);
        }

        await using PenguinPlankDbContext readContext = _fixture.CreateContext();
        var readStore = new EfCredentialStore(readContext, new FixedTimeProvider(revokedAt));
        Result<CredentialResolution> resolved = await readStore.ResolveAsync(id, CancellationToken.None);

        Assert.Equal(expected, resolved.Value.Decision);
        Assert.Null(resolved.Value.Reference);

        var connections = new EfIntegrationConnectionStore(readContext, new FixedTimeProvider(revokedAt));
        IntegrationConnectionSummary? summary = await connections.GetAsync(id, CancellationToken.None);
        Assert.Equal(state, summary?.CredentialState);
        Assert.Equal(revokedAt, summary?.CredentialStateChangedAtUtc);
    }

    [Fact]
    public async Task StashReferenceAsync_AfterRevocation_ReactivatesCredentialAsync()
    {
        Guid id = await RecordConnectionAsync();

        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var store = new EfCredentialStore(context, new FixedTimeProvider(s_start));
        await store.StashReferenceAsync(id, NewReference(), CancellationToken.None);
        await store.RecordCredentialStateAsync(id, CredentialState.Revoked, CancellationToken.None);

        await store.StashReferenceAsync(id, NewReference(), CancellationToken.None);
        Result<CredentialResolution> resolved = await store.ResolveAsync(id, CancellationToken.None);

        Assert.Equal(CredentialUseDecision.Usable, resolved.Value.Decision);
    }

    [Fact]
    public async Task RecordCredentialStateAsync_Active_RejectsWithValidationAsync()
    {
        Guid id = await RecordConnectionAsync();

        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var store = new EfCredentialStore(context, new FixedTimeProvider(s_start));
        Result result = await store.RecordCredentialStateAsync(id, CredentialState.Active, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_UnknownConnection_ReturnsValidationFailureAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var store = new EfCredentialStore(context, new FixedTimeProvider(s_start));

        Result<CredentialResolution> result = await store.ResolveAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    private async Task<Guid> RecordConnectionAsync()
    {
        Guid id = Guid.NewGuid();
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var store = new EfIntegrationConnectionStore(context, new FixedTimeProvider(s_start));
        await store.RecordAsync(id, NewRegistration(), CancellationToken.None);
        return id;
    }

    private static ConnectionRegistration NewRegistration()
    {
        return new ConnectionRegistration(
            "Shopify",
            "penguin-shop",
            "read_orders",
            "2026-07",
            "orders",
            SyncDirection.Inbound);
    }

    private static CredentialReference NewReference()
    {
        return CredentialReference.Create(Locator).Value;
    }

    /// <summary>A deterministic <see cref="TimeProvider"/> that always reports a fixed instant.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _instant;

        public FixedTimeProvider(DateTimeOffset instant)
        {
            _instant = instant;
        }

        public override DateTimeOffset GetUtcNow() => _instant;
    }
}
