using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Idempotency;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Infrastructure.Idempotency;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests asserting that idempotency-key uniqueness is enforced by the real database and
/// that <see cref="EfIdempotencyStore"/> serializes concurrent same-key requests through the unique
/// index on <c>IdempotencyRecord.Key</c> (requirement A4 Â§6.2), exercised against a real isolated
/// SQL Server database (coding-standards Â§7).
/// </summary>
/// <remarks>
/// Each test arranges only the rows it needs through the shared <see cref="SqlServerDatabaseFixture"/>
/// and asserts observable behavior: the store's New/Replay/Conflict outcome for a reused key, the
/// database rejecting a second row with the same key via its unique index (SQL error 2601/2627),
/// and exactly one of several concurrent same-key begins observing <see cref="IdempotencyOutcome.New"/>.
/// No test mocks a <c>DbSet</c>/<c>IQueryable</c>, because that cannot prove relational correctness.
/// </remarks>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class IdempotencyConcurrencyTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public IdempotencyConcurrencyTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // -----------------------------------------------------------------------------------------
    // Store-level New / Replay / Conflict outcomes (requirement A4 Â§6.2, Â§6.3, Â§6.4).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task BeginAsync_FirstUseOfKey_ReturnsNewAsync()
    {
        IdempotencyKey key = NewKey();
        PayloadHash hash = NewHash("payload-A");

        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var store = new EfIdempotencyStore(context, TimeProvider.System);

        IdempotencyOutcome outcome =
            await store.BeginAsync(key, NewCaller(), NewOperation(), hash, CancellationToken.None);

        Assert.Equal(IdempotencyOutcomeKind.New, outcome.Kind);
    }

    [Fact]
    public async Task BeginAsync_SameKeySamePayloadHash_ReturnsReplayAsync()
    {
        IdempotencyKey key = NewKey();
        PayloadHash hash = NewHash("payload-A");
        Caller caller = NewCaller();
        Operation operation = NewOperation();

        // First begin reserves the key in its own committed context.
        await using (PenguinPlankDbContext first = _fixture.CreateContext())
        {
            var firstStore = new EfIdempotencyStore(first, TimeProvider.System);
            IdempotencyOutcome firstOutcome =
                await firstStore.BeginAsync(key, caller, operation, hash, CancellationToken.None);
            Assert.Equal(IdempotencyOutcomeKind.New, firstOutcome.Kind);
        }

        // A second begin with the same key and the SAME payload hash is a replay.
        await using PenguinPlankDbContext second = _fixture.CreateContext();
        var secondStore = new EfIdempotencyStore(second, TimeProvider.System);
        IdempotencyOutcome replay =
            await secondStore.BeginAsync(key, caller, operation, hash, CancellationToken.None);

        Assert.Equal(IdempotencyOutcomeKind.Replay, replay.Kind);
    }

    [Fact]
    public async Task BeginAsync_SameKeyDifferentPayloadHash_ReturnsConflictAsync()
    {
        IdempotencyKey key = NewKey();
        Caller caller = NewCaller();
        Operation operation = NewOperation();

        await using (PenguinPlankDbContext first = _fixture.CreateContext())
        {
            var firstStore = new EfIdempotencyStore(first, TimeProvider.System);
            await firstStore.BeginAsync(
                key, caller, operation, NewHash("payload-A"), CancellationToken.None);
        }

        // A second begin with the same key but a DIFFERENT payload hash is a conflict (Â§6.3).
        await using PenguinPlankDbContext second = _fixture.CreateContext();
        var secondStore = new EfIdempotencyStore(second, TimeProvider.System);
        IdempotencyOutcome conflict = await secondStore.BeginAsync(
            key, caller, operation, NewHash("payload-B"), CancellationToken.None);

        Assert.Equal(IdempotencyOutcomeKind.Conflict, conflict.Kind);
    }

    [Fact]
    public async Task BeginAsync_ReplayAfterComplete_CarriesStoredResultAsync()
    {
        IdempotencyKey key = NewKey();
        PayloadHash hash = NewHash("payload-A");
        Caller caller = NewCaller();
        Operation operation = NewOperation();
        var result = new ResultRef("variant/" + Guid.NewGuid().ToString("N"));

        await using (PenguinPlankDbContext first = _fixture.CreateContext())
        {
            var firstStore = new EfIdempotencyStore(first, TimeProvider.System);
            await firstStore.BeginAsync(key, caller, operation, hash, CancellationToken.None);
            await firstStore.CompleteAsync(key, result, CancellationToken.None);
        }

        await using PenguinPlankDbContext second = _fixture.CreateContext();
        var secondStore = new EfIdempotencyStore(second, TimeProvider.System);
        IdempotencyOutcome replay =
            await secondStore.BeginAsync(key, caller, operation, hash, CancellationToken.None);

        Assert.Equal(IdempotencyOutcomeKind.Replay, replay.Kind);
        Assert.Equal(result, replay.StoredResult);
    }

    // -----------------------------------------------------------------------------------------
    // Database-level uniqueness (requirement A4 Â§6.2): two records with the same Key cannot both
    // commit â€” the second SaveChanges violates the unique index (SQL error 2601/2627).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_DuplicateIdempotencyKey_ViolatesUniqueIndexAsync()
    {
        string key = "idem-" + Guid.NewGuid().ToString("N");

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            context.IdempotencyRecords.Add(NewRecord(key, "hash-A"));
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext duplicateContext = _fixture.CreateContext();
        duplicateContext.IdempotencyRecords.Add(NewRecord(key, "hash-B"));

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());

        Assert.True(IsUniqueConstraintViolation(exception));

        // Exactly one row survives: the unique index let only the first commit through.
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.IdempotencyRecords.CountAsync(r => r.Key == key));
    }

    // -----------------------------------------------------------------------------------------
    // Concurrent same-key begins: exactly one observes New; the rest resolve to Replay/Conflict
    // against the single committed record. The DB unique index serializes the race (Â§6.2).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task BeginAsync_ConcurrentSameKeyRequests_ExactlyOneObservesNewAsync()
    {
        IdempotencyKey key = NewKey();
        PayloadHash hash = NewHash("payload-A");
        Caller caller = NewCaller();
        Operation operation = NewOperation();

        const int concurrency = 8;

        // Each racing request runs on its own context (its own connection), as separate requests do.
        var contexts = new PenguinPlankDbContext[concurrency];
        try
        {
            var begins = new Task<IdempotencyOutcome>[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                contexts[i] = _fixture.CreateContext();
                var store = new EfIdempotencyStore(contexts[i], TimeProvider.System);
                begins[i] = store.BeginAsync(key, caller, operation, hash, CancellationToken.None);
            }

            IdempotencyOutcome[] outcomes = await Task.WhenAll(begins);

            int newCount = outcomes.Count(outcome => outcome.Kind == IdempotencyOutcomeKind.New);
            Assert.Equal(1, newCount);

            // Every racing loser resolves to a replay (identical payload hash), never a conflict.
            Assert.All(
                outcomes.Where(outcome => outcome.Kind != IdempotencyOutcomeKind.New),
                outcome => Assert.Equal(IdempotencyOutcomeKind.Replay, outcome.Kind));
        }
        finally
        {
            foreach (PenguinPlankDbContext context in contexts)
            {
                await context.DisposeAsync();
            }
        }

        // Only one reserving row exists for the key despite the concurrent begins.
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.IdempotencyRecords.CountAsync(r => r.Key == key.Value));
    }

    // -----------------------------------------------------------------------------------------
    // Arrange helpers â€” minimal, explicit fixtures (no shared seeded dataset, Â§7).
    // -----------------------------------------------------------------------------------------

    private static IdempotencyKey NewKey() => new("idem-" + Guid.NewGuid().ToString("N"));

    private static PayloadHash NewHash(string seed) => new(seed + "-" + Guid.Empty.ToString("N"));

    private static Caller NewCaller() => new(Guid.NewGuid());

    private static Operation NewOperation() => new("CreateVariant");

    private static IdempotencyRecord NewRecord(string key, string payloadHash) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        CallerId = Guid.NewGuid(),
        Operation = "CreateVariant",
        PayloadHash = payloadHash,
        ResultReference = null,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    // SQL Server error 2601 = duplicate key row; 2627 = unique constraint/PK violation.
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Errors.Cast<SqlError>().Any(e => e.Number is 2601 or 2627);
}
