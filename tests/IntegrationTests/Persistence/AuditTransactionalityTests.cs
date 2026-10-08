using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Auditing;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests asserting that <see cref="AuditSaveChangesInterceptor"/> writes an
/// <see cref="AuditEntry"/> in the <b>same transaction</b> as the business mutation it describes:
/// the audit row commits if and only if the mutation commits (requirement A4 Â§6.7). Exercised
/// against a real isolated SQL Server database (coding-standards Â§7).
/// </summary>
/// <remarks>
/// <para>
/// The context under test is built directly on the fixture's isolated database with the interceptor
/// attached and a hand-written <see cref="IActorContextAccessor"/> supplying a known
/// <see cref="ActorContext"/> â€” a controllable substitute at a boundary (coding-standards Â§7), not a
/// mock of a <c>DbSet</c>.
/// </para>
/// <para>
/// Both directions of the iff are proven. A committed insert and a committed update each leave
/// exactly one matching audit row attributed to the known actor. A mutation performed inside an
/// explicit transaction that is then rolled back leaves <b>no</b> mutation row and <b>no</b> audit
/// row, because the audit insert the interceptor added rode in the same uncommitted transaction.
/// </para>
/// </remarks>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class AuditTransactionalityTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public AuditTransactionalityTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // -----------------------------------------------------------------------------------------
    // Commit direction: a successful mutation commits exactly one matching AuditEntry.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_CommittedInsert_WritesOneAuditEntryForTheActorAsync()
    {
        Guid actorId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        await using (PenguinPlankDbContext context = CreateAuditingContext(actorId, Role.Owner))
        {
            context.Products.Add(NewProduct(productId, "Audited insert"));
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();

        // Exactly one audit row for this entity, attributed to the known actor, describing a create.
        AuditEntry entry = await verify.AuditEntries
            .SingleAsync(e => e.EntityId == productId);

        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(nameof(Product), entry.EntityType);
        Assert.Equal(nameof(Product) + AuditOperation.Created, entry.Action);
    }

    [Fact]
    public async Task SaveChangesAsync_CommittedUpdate_WritesAnUpdateAuditEntryAsync()
    {
        Guid actorId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        // Seed the product WITHOUT the interceptor so the create is not itself audited.
        await using (PenguinPlankDbContext seed = _fixture.CreateContext())
        {
            seed.Products.Add(NewProduct(productId, "Before edit"));
            await seed.SaveChangesAsync();
        }

        await using (PenguinPlankDbContext context = CreateAuditingContext(actorId, Role.Owner))
        {
            Product tracked = await context.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "After edit";
            await context.SaveChangesAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        AuditEntry entry = await verify.AuditEntries.SingleAsync(e => e.EntityId == productId);

        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(nameof(Product) + AuditOperation.Updated, entry.Action);
        // The summary records the changed property NAME, never the value (Â§6.7).
        Assert.Contains(nameof(Product.Name), entry.PermittedChangeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("After edit", entry.PermittedChangeSummary, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------------------------
    // Rollback direction: when the mutation is rolled back, NO audit row persists. The audit
    // insert the interceptor added rides in the same uncommitted transaction.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_RolledBackMutation_PersistsNeitherMutationNorAuditEntryAsync()
    {
        Guid actorId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        await using (PenguinPlankDbContext context = CreateAuditingContext(actorId, Role.Owner))
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync();

            context.Products.Add(NewProduct(productId, "Rolled back"));

            // SaveChanges runs the interceptor (which adds the audit row) and writes both rows into
            // the open transaction â€” but nothing is committed.
            await context.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();

        // Neither the mutation nor its audit row survived the rollback â€” audit is atomic with it.
        Assert.False(await verify.Products.AnyAsync(p => p.Id == productId));
        Assert.False(await verify.AuditEntries.AnyAsync(e => e.EntityId == productId));
    }

    [Fact]
    public async Task SaveChangesAsync_CommittedTransaction_PersistsBothMutationAndAuditEntryAsync()
    {
        // The positive mirror of the rollback test: committing the SAME explicit-transaction flow
        // persists both rows, proving the rollback result is the transaction's doing, not a quirk.
        Guid actorId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        await using (PenguinPlankDbContext context = CreateAuditingContext(actorId, Role.Owner))
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync();

            context.Products.Add(NewProduct(productId, "Committed"));
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.True(await verify.Products.AnyAsync(p => p.Id == productId));
        Assert.True(await verify.AuditEntries.AnyAsync(e => e.EntityId == productId));
    }

    // -----------------------------------------------------------------------------------------
    // Arrange helpers.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a context bound to the isolated database with the audit interceptor attached and a
    /// controllable actor accessor returning the given actor â€” a boundary substitute, not a mock.
    /// </summary>
    private PenguinPlankDbContext CreateAuditingContext(Guid actorId, Role role)
    {
        var accessor = new TestActorContextAccessor();
        accessor.SetCurrent(new ActorContext(actorId, role));

        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>()
                .UseSqlServer(_fixture.ConnectionString)
                .AddInterceptors(new AuditSaveChangesInterceptor(accessor, TimeProvider.System))
                .Options;

        return new PenguinPlankDbContext(options);
    }

    private static Product NewProduct(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        PublicationState = PublicationState.Draft,
        ActiveFlag = true,
    };

    /// <summary>
    /// A hand-written <see cref="IActorContextAccessor"/> holding one actor for the test scope. A
    /// simple fake for a boundary is preferred over a mocking library here (coding-standards Â§7).
    /// </summary>
    private sealed class TestActorContextAccessor : IActorContextAccessor
    {
        public ActorContext? Current { get; private set; }

        public void SetCurrent(ActorContext actor)
        {
            ArgumentNullException.ThrowIfNull(actor);
            Current = actor;
        }
    }
}
