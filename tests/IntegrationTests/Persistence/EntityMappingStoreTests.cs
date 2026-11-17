using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Integration tests for <see cref="EfEntityMappingStore"/> against a real isolated SQL Server
/// database: identifier preservation, duplicate-triple rejection (requirement 3.4), and SKU matches
/// returned as suggestions without writing a mapping (requirement 3.5).
/// </summary>
[Collection(SqlServerDatabaseTestGroup.Name)]
public sealed class EntityMappingStoreTests
{
    private const string Platform = "Shopify";

    private readonly SqlServerDatabaseFixture _fixture;

    public EntityMappingStoreTests(SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MapAsync_NewTriple_PreservesIdentifiersAsync()
    {
        (Guid connectionId, string account) = await InsertConnectionAsync();
        var request = new EntityMappingRequest(connectionId, Platform, account, "gid://1", "ProductVariant", Guid.NewGuid());

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            Result<EntityMappingRecord> result =
                await new EfEntityMappingStore(context, TimeProvider.System).MapAsync(request, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        EntityMappingRecord? stored = await new EfEntityMappingStore(verify, TimeProvider.System)
            .FindAsync(Platform, account, "gid://1", CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(request.ExternalId, stored.ExternalId);
        Assert.Equal(request.Account, stored.Account);
        Assert.Equal(request.InternalId, stored.InternalId);
        Assert.Equal(request.OwnerType, stored.OwnerType);
    }

    [Fact]
    public async Task MapAsync_DuplicateTriple_RejectsAndKeepsOriginalAsync()
    {
        (Guid connectionId, string account) = await InsertConnectionAsync();
        var first = new EntityMappingRequest(connectionId, Platform, account, "gid://2", "ProductVariant", Guid.NewGuid());
        EntityMappingRequest second = first with { InternalId = Guid.NewGuid() };

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            await new EfEntityMappingStore(context, TimeProvider.System).MapAsync(first, CancellationToken.None);
        }

        await using PenguinPlankDbContext duplicateContext = _fixture.CreateContext();
        Result<EntityMappingRecord> result = await new EfEntityMappingStore(duplicateContext, TimeProvider.System)
            .MapAsync(second, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.MappingConflict, result.Error.Code);

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        EntityMapping stored = await verify.EntityMappings
            .SingleAsync(mapping => mapping.Account == account && mapping.ExternalId == "gid://2");
        Assert.Equal(first.InternalId, stored.InternalId);
    }

    [Fact]
    public async Task MapAsync_SameExternalIdDifferentAccount_MapsBothAsync()
    {
        (Guid firstConnectionId, string firstAccount) = await InsertConnectionAsync();
        (Guid secondConnectionId, string secondAccount) = await InsertConnectionAsync();
        var first = new EntityMappingRequest(firstConnectionId, Platform, firstAccount, "gid://6", "ProductVariant", Guid.NewGuid());
        var second = new EntityMappingRequest(secondConnectionId, Platform, secondAccount, "gid://6", "ProductVariant", Guid.NewGuid());

        Result<EntityMappingRecord> firstResult;
        Result<EntityMappingRecord> secondResult;
        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            firstResult = await new EfEntityMappingStore(context, TimeProvider.System).MapAsync(first, CancellationToken.None);
        }

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            secondResult = await new EfEntityMappingStore(context, TimeProvider.System).MapAsync(second, CancellationToken.None);
        }

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.Equal(2, await verify.EntityMappings.CountAsync(mapping =>
            (mapping.Account == firstAccount || mapping.Account == secondAccount) && mapping.ExternalId == "gid://6"));
    }

    [Fact]
    public async Task MapAsync_ConcurrentSameTriple_ExactlyOneSucceedsAsync()
    {
        (Guid connectionId, string account) = await InsertConnectionAsync();

        Task<Result<EntityMappingRecord>>[] attempts = Enumerable.Range(0, 4)
            .Select(async _ =>
            {
                await using PenguinPlankDbContext context = _fixture.CreateContext();
                var request = new EntityMappingRequest(connectionId, Platform, account, "gid://3", "ProductVariant", Guid.NewGuid());
                return await new EfEntityMappingStore(context, TimeProvider.System).MapAsync(request, CancellationToken.None);
            })
            .ToArray();

        Result<EntityMappingRecord>[] results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => result.IsFailure), result => Assert.Equal(ErrorCode.MappingConflict, result.Error.Code));
    }

    [Fact]
    public async Task MapAsync_AccountDiffersFromConnection_FailsValidationAsync()
    {
        (Guid connectionId, _) = await InsertConnectionAsync();
        var request = new EntityMappingRequest(connectionId, Platform, "other-shop", "gid://4", "ProductVariant", Guid.NewGuid());

        await using PenguinPlankDbContext context = _fixture.CreateContext();
        Result<EntityMappingRecord> result =
            await new EfEntityMappingStore(context, TimeProvider.System).MapAsync(request, CancellationToken.None);

        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    [Fact]
    public async Task SuggestSkuMatchesAsync_MatchingSku_ReturnsSuggestionWithoutWritingMappingAsync()
    {
        (Guid connectionId, string account) = await InsertConnectionAsync();
        string sku = $"SKU-{Guid.NewGuid():N}";
        Guid variantId = await InsertVariantAsync(sku);

        await using (PenguinPlankDbContext context = _fixture.CreateContext())
        {
            Result<IReadOnlyList<MappingSuggestion>> result = await new EfEntityMappingStore(context, TimeProvider.System)
                .SuggestSkuMatchesAsync(connectionId, [new ExternalSkuCandidate("gid://5", sku)], CancellationToken.None);

            MappingSuggestion suggestion = Assert.Single(result.Value);
            Assert.Equal(variantId, suggestion.VariantId);
        }

        await using PenguinPlankDbContext verify = _fixture.CreateContext();
        Assert.False(await verify.EntityMappings.AnyAsync(mapping => mapping.Account == account));
    }

    private async Task<(Guid ConnectionId, string Account)> InsertConnectionAsync()
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var connection = new IntegrationConnection
        {
            Id = Guid.NewGuid(),
            Platform = Platform,
            Account = $"shop-{Guid.NewGuid():N}",
            Scopes = "read_products",
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

    private async Task<Guid> InsertVariantAsync(string sku)
    {
        await using PenguinPlankDbContext context = _fixture.CreateContext();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "End-grain cutting board",
            PublicationState = PublicationState.Draft,
            ActiveFlag = true,
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Sku = sku,
            TrackingMode = TrackingMode.Quantity,
            UnitOfMeasure = "each",
        };
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync();
        return variant.Id;
    }
}
