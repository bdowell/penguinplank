using Microsoft.EntityFrameworkCore;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.Persistence;
using PenguinPlank.Infrastructure.Persistence.Concurrency;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests asserting that <see cref="EfConcurrentUpdateExecutor"/> honors the client's
/// <c>If-Match</c> row version against the real database: a stale version is refused with
/// <see cref="ErrorCode.StaleVersion"/> and never overwrites newer state, while a current version
/// commits (requirements A4 Â§6.5, Â§6.6). Exercised against a real isolated SQL Server database
/// (coding-standards Â§7).
/// </summary>
/// <remarks>
/// Each test inserts a single <see cref="Product"/> (a <see cref="VersionedEntity"/> with a SQL
/// <c>rowversion</c> token), captures its ETag, then performs a concurrent update through a second
/// context that advances the stored row version. A follow-up update that still presents the stale
/// token must fail without touching the stored row; a follow-up that reads and presents the current
/// token must succeed. No test mocks a <c>DbSet</c>/<c>IQueryable</c>, because that cannot prove the
/// conditional <c>UPDATE ... WHERE rowversion = @ifMatch</c> semantics the executor relies on.
/// </remarks>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class EtagConcurrencyTests
{
    private readonly SqlServerDatabaseFixture _fixture;

    public EtagConcurrencyTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_StaleRowVersion_FailsWithStaleVersionAndDoesNotOverwriteAsync()
    {
        Guid productId = await InsertProductAsync("Original name");

        // The client reads the aggregate and remembers its ETag (the If-Match token it will send).
        byte[] staleRowVersion;
        await using (PenguinPlankDbContext read = _fixture.CreateContext())
        {
            Product loaded = await read.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
            staleRowVersion = loaded.RowVersion;
        }

        // A concurrent writer edits the same row, advancing its stored row version.
        await using (PenguinPlankDbContext concurrent = _fixture.CreateContext())
        {
            Product tracked = await concurrent.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "Winner's name";
            await concurrent.SaveChangesAsync();
        }

        // The original client now attempts its edit with the STALE token: it must be refused.
        await using (PenguinPlankDbContext editContext = _fixture.CreateContext())
        {
            Product tracked = await editContext.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "Loser's stale name";

            var executor = new EfConcurrentUpdateExecutor(editContext);
            Result result = await executor.ExecuteAsync(
                tracked, staleRowVersion, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCode.StaleVersion, result.Error.Code);
        }

        // The newer value survives: the stale edit never overwrote the concurrent writer's change.
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Product stored = await verify.Products.SingleAsync(p => p.Id == productId);
        Assert.Equal("Winner's name", stored.Name);
    }

    [Fact]
    public async Task ExecuteAsync_StaleRowVersion_LeavesContextUsableForLaterSaveAsync()
    {
        // Regression: the stale edit's pending changes stayed tracked, so the next SaveChanges on the
        // same scoped context (the idempotency pipeline recording the outcome) retried the stale
        // UPDATE, threw, and turned the 412 into a 500.
        Guid productId = await InsertProductAsync("Original name");

        byte[] staleRowVersion;
        await using (PenguinPlankDbContext read = _fixture.CreateContext())
        {
            staleRowVersion = (await read.Products.AsNoTracking().SingleAsync(p => p.Id == productId)).RowVersion;
        }

        await using (PenguinPlankDbContext concurrent = _fixture.CreateContext())
        {
            Product tracked = await concurrent.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "Winner's name";
            await concurrent.SaveChangesAsync();
        }

        Guid laterProductId = Guid.NewGuid();
        await using (PenguinPlankDbContext editContext = _fixture.CreateContext())
        {
            Product tracked = await editContext.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "Loser's stale name";
            Result result = await new EfConcurrentUpdateExecutor(editContext)
                .ExecuteAsync(tracked, staleRowVersion, CancellationToken.None);
            Assert.Equal(ErrorCode.StaleVersion, result.Error.Code);

            // A later, unrelated write on the same context must succeed.
            editContext.Products.Add(new Product
            {
                Id = laterProductId,
                Name = "Later write",
                PublicationState = PublicationState.Draft,
                ActiveFlag = true,
            });
            await editContext.SaveChangesAsync();
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal("Winner's name", (await verify.Products.SingleAsync(p => p.Id == productId)).Name);
        Assert.True(await verify.Products.AnyAsync(p => p.Id == laterProductId));
    }

    [Fact]
    public async Task ExecuteAsync_CurrentRowVersion_SucceedsAndAppliesTheEditAsync()
    {
        Guid productId = await InsertProductAsync("Original name");

        await using (PenguinPlankDbContext editContext = _fixture.CreateContext())
        {
            Product tracked = await editContext.Products.SingleAsync(p => p.Id == productId);
            byte[] currentRowVersion = tracked.RowVersion;
            tracked.Name = "Edited name";

            var executor = new EfConcurrentUpdateExecutor(editContext);
            Result result = await executor.ExecuteAsync(
                tracked, currentRowVersion, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Product stored = await verify.Products.SingleAsync(p => p.Id == productId);
        Assert.Equal("Edited name", stored.Name);
    }

    [Fact]
    public async Task ExecuteAsync_SecondEditWithPreviousVersion_FailsAfterFirstSucceedsAsync()
    {
        Guid productId = await InsertProductAsync("Original name");

        // Capture the first version the client sees.
        byte[] firstVersion;
        await using (PenguinPlankDbContext read = _fixture.CreateContext())
        {
            Product loaded = await read.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
            firstVersion = loaded.RowVersion;
        }

        // The first edit with the current version succeeds and advances the stored row version.
        await using (PenguinPlankDbContext firstEdit = _fixture.CreateContext())
        {
            Product tracked = await firstEdit.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "First edit";
            var executor = new EfConcurrentUpdateExecutor(firstEdit);
            Result result = await executor.ExecuteAsync(tracked, firstVersion, CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        // Reusing the now-superseded first version must be refused as stale.
        await using (PenguinPlankDbContext secondEdit = _fixture.CreateContext())
        {
            Product tracked = await secondEdit.Products.SingleAsync(p => p.Id == productId);
            tracked.Name = "Second edit on stale version";
            var executor = new EfConcurrentUpdateExecutor(secondEdit);
            Result result = await executor.ExecuteAsync(tracked, firstVersion, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCode.StaleVersion, result.Error.Code);
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Product stored = await verify.Products.SingleAsync(p => p.Id == productId);
        Assert.Equal("First edit", stored.Name);
    }

    private async Task<Guid> InsertProductAsync(string name)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            PublicationState = PublicationState.Draft,
            ActiveFlag = true,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product.Id;
    }
}
