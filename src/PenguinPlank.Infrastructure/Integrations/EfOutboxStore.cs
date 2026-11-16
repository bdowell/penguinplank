using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="IOutboxStore"/> over the <c>IntegrationOutbox</c>
/// table (requirement 3.14).
/// </summary>
/// <remarks>
/// The store adds the job to the <b>scoped <see cref="PenguinPlankDbContext"/> shared with the
/// business change</b> and never calls <c>SaveChanges</c>. The owning use case's single commit
/// (or its explicit transaction) therefore persists the job and the change atomically, and a
/// rollback or failed commit discards both. The unique index on <c>OperationKey</c> remains the
/// final guard against a concurrent enqueue of the same key.
/// </remarks>
public sealed class EfOutboxStore : IOutboxStore
{
    private readonly PenguinPlankDbContext _dbContext;
    private readonly System.TimeProvider _timeProvider;

    /// <summary>Creates the store over the database context and time source.</summary>
    /// <param name="dbContext">The scoped application database context shared with the business change.</param>
    /// <param name="timeProvider">The injected time source for timestamps.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="dbContext"/> or <paramref name="timeProvider"/> is null.
    /// </exception>
    public EfOutboxStore(PenguinPlankDbContext dbContext, System.TimeProvider timeProvider)
    {
        System.ArgumentNullException.ThrowIfNull(dbContext);
        System.ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<OutboxEnqueueOutcome> EnqueueAsync(
        OutboxJob job,
        CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(job);

        string key = job.OperationKey.Value;

        // Jobs staged earlier in this unit of work are not yet visible to the database query.
        bool stagedInThisUnit = _dbContext.IntegrationOutbox.Local
            .Any(item => item.OperationKey == key);

        if (stagedInThisUnit)
        {
            return OutboxEnqueueOutcome.AlreadyEnqueued;
        }

        bool exists = await _dbContext.IntegrationOutbox
            .AnyAsync(item => item.OperationKey == key, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return OutboxEnqueueOutcome.AlreadyEnqueued;
        }

        System.DateTimeOffset now = _timeProvider.GetUtcNow();

        _dbContext.IntegrationOutbox.Add(new IntegrationOutbox
        {
            Id = System.Guid.NewGuid(),
            ConnectionId = job.ConnectionId,
            OperationKey = key,
            PayloadReference = job.Payload.Value,
            Status = OutboxStatus.Pending,
            Attempts = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        return OutboxEnqueueOutcome.Staged;
    }
}
