using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Idempotency;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Idempotency;

/// <summary>
/// The EF Core implementation of <see cref="IIdempotencyStore"/> over the
/// <see cref="PenguinPlankDbContext.IdempotencyRecords"/> table, using the <b>unique index on
/// the key</b> to serialize concurrent same-key requests (requirements A4 §6.2–§6.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>How database uniqueness serializes concurrency.</b> When several requests arrive with the
/// same key at once, each tries to insert a reserving record. The unique index on
/// <c>IdempotencyRecord.Key</c> permits exactly one insert to commit — that request observes
/// <see cref="IdempotencyOutcomeKind.New"/> and proceeds to run the command. Every racing insert
/// fails with a duplicate-key error; this store catches that specific failure, re-reads the
/// now-committed record, and asks the pure <see cref="IdempotencyDecision"/> for the outcome — a
/// replay when the payload hash matches, a conflict when it differs. The business decision is thus
/// never duplicated in infrastructure; the store only translates storage facts into inputs for the
/// rule.
/// </para>
/// <para>
/// The store depends on the scoped <see cref="PenguinPlankDbContext"/> and an injected
/// <see cref="System.TimeProvider"/> for timestamps (coding-standards §2; requirement A8 §10.1),
/// so it is registered scoped and reads no clock directly. All queries propagate the
/// <see cref="CancellationToken"/>, and expected outcomes are returned as values rather than
/// thrown (coding-standards §6).
/// </para>
/// </remarks>
public sealed class EfIdempotencyStore : IIdempotencyStore
{
    // SQL Server duplicate-key error numbers: 2627 (unique constraint) and 2601 (unique index).
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    private readonly PenguinPlankDbContext _dbContext;
    private readonly System.TimeProvider _timeProvider;

    /// <summary>Creates the store over the database context and time source.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    /// <param name="timeProvider">The injected time source for record timestamps.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="dbContext"/> or <paramref name="timeProvider"/> is null.
    /// </exception>
    public EfIdempotencyStore(PenguinPlankDbContext dbContext, System.TimeProvider timeProvider)
    {
        System.ArgumentNullException.ThrowIfNull(dbContext);
        System.ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<IdempotencyOutcome> BeginAsync(
        IdempotencyKey key,
        Caller caller,
        Operation operation,
        PayloadHash payloadHash,
        CancellationToken cancellationToken)
    {
        // A record may already exist from a prior request with this key; if so, decide replay vs
        // conflict without attempting an insert that we know would violate the unique index.
        IdempotencyRecord? existing = await FindByKeyAsync(key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return DecideForExisting(existing, payloadHash);
        }

        System.DateTimeOffset now = _timeProvider.GetUtcNow();

        var record = new IdempotencyRecord
        {
            Id = System.Guid.NewGuid(),
            Key = key.Value,
            CallerId = caller.Id,
            Operation = operation.Name,
            PayloadHash = payloadHash.Value,
            ResultReference = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _dbContext.IdempotencyRecords.Add(record);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return IdempotencyOutcome.New;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent request won the race to reserve this key. Detach our failed insert and
            // resolve the outcome against the record that actually committed.
            _dbContext.Entry(record).State = EntityState.Detached;

            IdempotencyRecord? committed = await FindByKeyAsync(key, cancellationToken)
                .ConfigureAwait(false);

            if (committed is null)
            {
                // The duplicate-key failure means a row with this key exists; not finding it would
                // be an inconsistent store, which is an unexpected failure rather than a business
                // outcome (coding-standards §6).
                throw new System.InvalidOperationException(
                    "A duplicate idempotency key was reported but no committed record was found.",
                    exception);
            }

            return DecideForExisting(committed, payloadHash);
        }
    }

    /// <inheritdoc />
    public async Task CompleteAsync(
        IdempotencyKey key,
        ResultRef result,
        CancellationToken cancellationToken)
    {
        IdempotencyRecord? record = await FindByKeyAsync(key, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            // CompleteAsync is only called after a New outcome reserved the key, so a missing
            // record is a broken protocol, not an expected business failure (coding-standards §6).
            throw new System.InvalidOperationException(
                $"No idempotency record exists for key '{key.Value}' to complete.");
        }

        record.ResultReference = result.Value;
        record.UpdatedAtUtc = _timeProvider.GetUtcNow();

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<IdempotencyRecord?> FindByKeyAsync(
        IdempotencyKey key,
        CancellationToken cancellationToken)
    {
        return _dbContext.IdempotencyRecords
            .SingleOrDefaultAsync(record => record.Key == key.Value, cancellationToken);
    }

    private static IdempotencyOutcome DecideForExisting(
        IdempotencyRecord existing,
        PayloadHash incomingHash)
    {
        ResultRef? storedResult = string.IsNullOrEmpty(existing.ResultReference)
            ? null
            : new ResultRef(existing.ResultReference);

        return IdempotencyDecision.ForExisting(
            new PayloadHash(existing.PayloadHash),
            incomingHash,
            storedResult);
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && (sqlException.Number == UniqueConstraintViolation
                || sqlException.Number == UniqueIndexViolation);
    }
}
