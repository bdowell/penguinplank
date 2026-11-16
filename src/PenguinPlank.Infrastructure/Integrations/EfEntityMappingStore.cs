using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="IEntityMappingStore"/>. The composite unique index on
/// (Platform, Account, ExternalId) is the authority on uniqueness; a pre-check gives the common
/// duplicate case a cheap answer and the duplicate-key catch covers concurrent writers
/// (requirement R11 / 3.4). SKU matches are returned as suggestions and never written (3.5).
/// </summary>
public sealed class EfEntityMappingStore : IEntityMappingStore
{
    // SQL Server duplicate-key error numbers: 2627 (unique constraint) and 2601 (unique index).
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    private readonly PenguinPlankDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the store over the database context and time source.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    /// <param name="timeProvider">The injected time source for record timestamps.</param>
    public EfEntityMappingStore(PenguinPlankDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<Result<EntityMappingRecord>> MapAsync(
        EntityMappingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result validation = EntityMappingValidator.Validate(request);
        if (validation.IsFailure)
        {
            return Result.Failure<EntityMappingRecord>(validation.Error);
        }

        IntegrationConnection? connection = await _dbContext.IntegrationConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ConnectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null
            || !string.Equals(connection.Platform, request.Platform, StringComparison.Ordinal)
            || !string.Equals(connection.Account, request.Account, StringComparison.Ordinal))
        {
            return Result.Failure<EntityMappingRecord>(
                ErrorCode.Validation,
                "The integration connection does not exist or does not match the platform and account.");
        }

        bool exists = await _dbContext.EntityMappings
            .AnyAsync(
                mapping => mapping.Platform == request.Platform
                    && mapping.Account == request.Account
                    && mapping.ExternalId == request.ExternalId,
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return DuplicateTriple();
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var mapping = new EntityMapping
        {
            Id = Guid.NewGuid(),
            ConnectionId = request.ConnectionId,
            Platform = request.Platform,
            Account = request.Account,
            ExternalId = request.ExternalId,
            OwnerType = request.OwnerType,
            InternalId = request.InternalId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _dbContext.EntityMappings.Add(mapping);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent writer committed the same triple first. Detach our failed insert so
            // the context stays usable.
            _dbContext.Entry(mapping).State = EntityState.Detached;
            return DuplicateTriple();
        }

        return Result.Success(ToRecord(mapping));
    }

    /// <inheritdoc />
    public async Task<EntityMappingRecord?> FindAsync(
        string platform,
        string account,
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(externalId);

        EntityMapping? mapping = await _dbContext.EntityMappings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Platform == platform
                    && candidate.Account == account
                    && candidate.ExternalId == externalId,
                cancellationToken)
            .ConfigureAwait(false);

        return mapping is null ? null : ToRecord(mapping);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MappingSuggestion>>> SuggestSkuMatchesAsync(
        Guid connectionId,
        IReadOnlyList<ExternalSkuCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        IntegrationConnection? connection = await _dbContext.IntegrationConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result.Failure<IReadOnlyList<MappingSuggestion>>(
                ErrorCode.Validation,
                "The integration connection does not exist.");
        }

        List<string> skus = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Sku))
            .Select(candidate => candidate.Sku.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<string> externalIds = candidates.Select(candidate => candidate.ExternalId).Distinct().ToList();

        if (skus.Count == 0)
        {
            return Result.Success<IReadOnlyList<MappingSuggestion>>([]);
        }

        List<InternalVariantSku> variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => skus.Contains(variant.Sku))
            .Select(variant => new InternalVariantSku(variant.Id, variant.Sku))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string platform = connection.Platform;
        string account = connection.Account;
        List<string> mappedIds = await _dbContext.EntityMappings
            .AsNoTracking()
            .Where(mapping => mapping.Platform == platform
                && mapping.Account == account
                && externalIds.Contains(mapping.ExternalId))
            .Select(mapping => mapping.ExternalId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MappingSuggestion> suggestions =
            SkuMatchSuggester.Suggest(candidates, variants, mappedIds.ToHashSet(StringComparer.Ordinal));

        return Result.Success(suggestions);
    }

    private static Result<EntityMappingRecord> DuplicateTriple()
    {
        return Result.Failure<EntityMappingRecord>(
            ErrorCode.MappingConflict,
            "The external resource is already mapped for this platform and account.");
    }

    private static EntityMappingRecord ToRecord(EntityMapping mapping)
    {
        return new EntityMappingRecord(
            mapping.Id,
            mapping.ConnectionId,
            mapping.Platform,
            mapping.Account,
            mapping.ExternalId,
            mapping.OwnerType,
            mapping.InternalId);
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && (sqlException.Number == UniqueConstraintViolation
                || sqlException.Number == UniqueIndexViolation);
    }
}
