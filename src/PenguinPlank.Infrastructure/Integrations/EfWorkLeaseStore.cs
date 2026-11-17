using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PenguinPlank.Application.Integrations.Worker;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="IWorkLeaseStore"/> over <c>IntegrationInbox</c> and
/// <c>IntegrationOutbox</c> (requirement 3.15).
/// </summary>
/// <remarks>
/// <para>
/// Candidate rows are selected with <c>UPDLOCK, READPAST</c> inside a transaction, so two workers
/// never lease the same row. Rows are then changed through the tracked context and
/// <c>SaveChanges</c>, so the audit interceptor records every worker mutation with the system
/// actor the caller established (requirement A7 9.6).
/// </para>
/// <para>
/// No retry-time column exists, so <c>LeaseExpiresAtUtc</c> doubles as the "not before" instant:
/// while an item is leased it is the lease expiry (an expired value reclaims a crashed item), and
/// after a failure it is the earliest next attempt. A leased row is counted as an attempt when the
/// lease is taken, which keeps attempt history correct after a crash.
/// </para>
/// </remarks>
public sealed class EfWorkLeaseStore : IWorkLeaseStore
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the database context.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    public EfWorkLeaseStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeasedWorkItem>> LeaseNextAsync(
        string leaseOwner,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        int batchSize,
        int maxTotalAttempts,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        var leased = new List<LeasedWorkItem>();

        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> inboxIds = await _dbContext.Database
            .SqlQuery<Guid>($"""
                SELECT TOP ({batchSize}) i.Id AS Value
                FROM IntegrationInbox AS i WITH (UPDLOCK, READPAST, ROWLOCK)
                INNER JOIN IntegrationConnections AS c ON c.Id = i.ConnectionId
                WHERE c.Enabled = 1 AND c.IsPaused = 0
                  AND i.Attempts < {maxTotalAttempts}
                  AND i.Status IN ('Received', 'Processing', 'Failed')
                  AND (i.LeaseExpiresAtUtc IS NULL OR i.LeaseExpiresAtUtc <= {nowUtc})
                ORDER BY i.CreatedAtUtc
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<IntegrationInbox> inboxItems = await _dbContext.IntegrationInbox
            .Where(item => inboxIds.Contains(item.Id))
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (IntegrationInbox item in inboxItems)
        {
            item.Status = InboxStatus.Processing;
            item.Attempts++;
            item.LeaseOwner = leaseOwner;
            item.LeaseExpiresAtUtc = leaseExpiresAtUtc;
            item.UpdatedAtUtc = nowUtc;
            leased.Add(new LeasedWorkItem(
                WorkItemKind.Inbox, item.Id, item.ConnectionId, item.PayloadReference, item.Attempts));
        }

        int outboxRoom = batchSize - inboxItems.Count;

        if (outboxRoom > 0)
        {
            List<Guid> outboxIds = await _dbContext.Database
                .SqlQuery<Guid>($"""
                    SELECT TOP ({outboxRoom}) o.Id AS Value
                    FROM IntegrationOutbox AS o WITH (UPDLOCK, READPAST, ROWLOCK)
                    INNER JOIN IntegrationConnections AS c ON c.Id = o.ConnectionId
                    WHERE c.Enabled = 1 AND c.IsPaused = 0
                      AND o.Attempts < {maxTotalAttempts}
                      AND o.Status IN ('Pending', 'Dispatching', 'Failed')
                      AND (o.LeaseExpiresAtUtc IS NULL OR o.LeaseExpiresAtUtc <= {nowUtc})
                    ORDER BY o.CreatedAtUtc
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<IntegrationOutbox> outboxItems = await _dbContext.IntegrationOutbox
                .Where(item => outboxIds.Contains(item.Id))
                .OrderBy(item => item.CreatedAtUtc)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (IntegrationOutbox item in outboxItems)
            {
                item.Status = OutboxStatus.Dispatching;
                item.Attempts++;
                item.LeaseOwner = leaseOwner;
                item.LeaseExpiresAtUtc = leaseExpiresAtUtc;
                item.UpdatedAtUtc = nowUtc;
                leased.Add(new LeasedWorkItem(
                    WorkItemKind.Outbox, item.Id, item.ConnectionId, item.PayloadReference, item.Attempts));
            }
        }

        if (leased.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return leased;
    }

    /// <inheritdoc />
    public Task<bool> CompleteAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        return ApplyAsync(
            item,
            leaseOwner,
            inbox =>
            {
                inbox.Status = InboxStatus.Processed;
                inbox.LeaseOwner = null;
                inbox.LeaseExpiresAtUtc = null;
                inbox.UpdatedAtUtc = nowUtc;
            },
            outbox =>
            {
                outbox.Status = OutboxStatus.Dispatched;
                outbox.LeaseOwner = null;
                outbox.LeaseExpiresAtUtc = null;
                outbox.UpdatedAtUtc = nowUtc;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RescheduleAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset retryAtUtc,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        return ApplyAsync(
            item,
            leaseOwner,
            inbox =>
            {
                inbox.Status = InboxStatus.Failed;
                inbox.LeaseOwner = null;
                inbox.LeaseExpiresAtUtc = retryAtUtc;
                inbox.UpdatedAtUtc = nowUtc;
            },
            outbox =>
            {
                outbox.Status = OutboxStatus.Failed;
                outbox.LeaseOwner = null;
                outbox.LeaseExpiresAtUtc = retryAtUtc;
                outbox.UpdatedAtUtc = nowUtc;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ReleaseAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        return ApplyAsync(
            item,
            leaseOwner,
            inbox =>
            {
                inbox.Status = InboxStatus.Received;
                inbox.Attempts--;
                inbox.LeaseOwner = null;
                inbox.LeaseExpiresAtUtc = null;
                inbox.UpdatedAtUtc = nowUtc;
            },
            outbox =>
            {
                outbox.Status = OutboxStatus.Pending;
                outbox.Attempts--;
                outbox.LeaseOwner = null;
                outbox.LeaseExpiresAtUtc = null;
                outbox.UpdatedAtUtc = nowUtc;
            },
            cancellationToken);
    }

    private async Task<bool> ApplyAsync(
        LeasedWorkItem item,
        string leaseOwner,
        Action<IntegrationInbox> applyToInbox,
        Action<IntegrationOutbox> applyToOutbox,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        // The owner and attempt number identify this lease; a reclaimed item no longer matches.
        if (item.Kind == WorkItemKind.Inbox)
        {
            IntegrationInbox? inbox = await _dbContext.IntegrationInbox
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == item.Id
                        && candidate.LeaseOwner == leaseOwner
                        && candidate.Status == InboxStatus.Processing
                        && candidate.Attempts == item.AttemptNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            if (inbox is null)
            {
                return false;
            }

            applyToInbox(inbox);
        }
        else
        {
            IntegrationOutbox? outbox = await _dbContext.IntegrationOutbox
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == item.Id
                        && candidate.LeaseOwner == leaseOwner
                        && candidate.Status == OutboxStatus.Dispatching
                        && candidate.Attempts == item.AttemptNumber,
                    cancellationToken)
                .ConfigureAwait(false);

            if (outbox is null)
            {
                return false;
            }

            applyToOutbox(outbox);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
