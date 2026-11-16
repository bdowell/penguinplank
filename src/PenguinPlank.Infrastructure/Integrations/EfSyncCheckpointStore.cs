using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="ISyncCheckpointStore"/> (requirement 3.9). The cursor
/// moves with an atomic compare-and-set in SQL, so a stale poller cannot overwrite newer progress.
/// The unique (connection, resource) index is the final guard when two pollers create the first
/// checkpoint at the same time.
/// </summary>
public sealed class EfSyncCheckpointStore : ISyncCheckpointStore
{
    // SQL Server duplicate-key error numbers: 2627 (unique constraint) and 2601 (unique index).
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the database context.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    public EfSyncCheckpointStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<SyncCheckpointState?> GetAsync(
        Guid connectionId,
        string resource,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        SyncCheckpoint? checkpoint = await _dbContext.SyncCheckpoints
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.ConnectionId == connectionId && candidate.Resource == resource,
                cancellationToken)
            .ConfigureAwait(false);

        return checkpoint is null
            ? null
            : new SyncCheckpointState(
                checkpoint.ConnectionId,
                checkpoint.Resource,
                checkpoint.Cursor,
                checkpoint.LastReconciledAtUtc);
    }

    /// <inheritdoc />
    public async Task<CheckpointAdvanceOutcome> AdvanceAsync(
        Guid connectionId,
        string resource,
        string? expectedCursor,
        string newCursor,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(newCursor);

        int changed = await _dbContext.SyncCheckpoints
            .Where(candidate => candidate.ConnectionId == connectionId
                && candidate.Resource == resource
                && candidate.Cursor == expectedCursor)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Cursor, newCursor)
                    .SetProperty(candidate => candidate.UpdatedAtUtc, nowUtc),
                cancellationToken)
            .ConfigureAwait(false);

        if (changed == 1)
        {
            return CheckpointAdvanceOutcome.Advanced;
        }

        // Nothing matched: a first-ever advance creates the checkpoint, any other caller is stale.
        if (expectedCursor is not null)
        {
            return CheckpointAdvanceOutcome.StaleCursor;
        }

        bool created = await TryCreateAsync(
            NewCheckpoint(connectionId, resource, newCursor, null, nowUtc),
            cancellationToken).ConfigureAwait(false);

        return created ? CheckpointAdvanceOutcome.Advanced : CheckpointAdvanceOutcome.StaleCursor;
    }

    /// <inheritdoc />
    public async Task MarkReconciledAsync(
        Guid connectionId,
        string resource,
        DateTimeOffset reconciledAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        if (await TryMarkAsync(connectionId, resource, reconciledAtUtc, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        bool created = await TryCreateAsync(
            NewCheckpoint(connectionId, resource, null, reconciledAtUtc, reconciledAtUtc),
            cancellationToken).ConfigureAwait(false);

        if (!created)
        {
            // A concurrent writer created the checkpoint first; stamp the existing row.
            await TryMarkAsync(connectionId, resource, reconciledAtUtc, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SyncCheckpoint NewCheckpoint(
        Guid connectionId,
        string resource,
        string? cursor,
        DateTimeOffset? reconciledAtUtc,
        DateTimeOffset nowUtc)
    {
        return new SyncCheckpoint
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            Resource = resource,
            Cursor = cursor,
            LastReconciledAtUtc = reconciledAtUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && (sqlException.Number == UniqueConstraintViolation
                || sqlException.Number == UniqueIndexViolation);
    }

    private async Task<bool> TryMarkAsync(
        Guid connectionId,
        string resource,
        DateTimeOffset reconciledAtUtc,
        CancellationToken cancellationToken)
    {
        int changed = await _dbContext.SyncCheckpoints
            .Where(candidate => candidate.ConnectionId == connectionId && candidate.Resource == resource)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.LastReconciledAtUtc, reconciledAtUtc)
                    .SetProperty(candidate => candidate.UpdatedAtUtc, reconciledAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return changed == 1;
    }

    private async Task<bool> TryCreateAsync(SyncCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        _dbContext.SyncCheckpoints.Add(checkpoint);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            _dbContext.Entry(checkpoint).State = EntityState.Detached;
            return false;
        }
    }
}
