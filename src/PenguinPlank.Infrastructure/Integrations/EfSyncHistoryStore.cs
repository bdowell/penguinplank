using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="ISyncHistoryStore"/> over the SyncRuns and
/// SyncExceptions tables (requirements 3.11, 3.12). It never deletes history.
/// </summary>
public sealed class EfSyncHistoryStore : ISyncHistoryStore
{
    private const int MaximumListedExceptions = 200;

    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the database context.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    public EfSyncHistoryStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task BeginRunAsync(NewSyncRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        _dbContext.SyncRuns.Add(new SyncRun
        {
            Id = run.Id,
            ConnectionId = run.ConnectionId,
            Resource = run.Resource,
            StartedAtUtc = run.StartedAtUtc,
            CreatedAtUtc = run.StartedAtUtc,
            UpdatedAtUtc = run.StartedAtUtc,
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> CompleteRunAsync(SyncRunCompletion completion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);

        SyncRun? run = await _dbContext.SyncRuns
            .SingleOrDefaultAsync(candidate => candidate.Id == completion.RunId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return false;
        }

        run.ProcessedCount = completion.ProcessedCount;
        run.FailedCount = completion.FailedCount;
        run.CompletedAtUtc = completion.CompletedAtUtc;
        run.LastSuccessAtUtc = completion.LastSuccessAtUtc;
        run.UpdatedAtUtc = completion.CompletedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> QueueExceptionAsync(SyncExceptionDraft exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        bool runExists = await _dbContext.SyncRuns
            .AnyAsync(candidate => candidate.Id == exception.SyncRunId, cancellationToken)
            .ConfigureAwait(false);

        if (!runExists)
        {
            return false;
        }

        _dbContext.SyncExceptions.Add(new SyncException
        {
            Id = exception.Id,
            SyncRunId = exception.SyncRunId,
            AffectedResource = exception.AffectedResource,
            RedactedError = exception.RedactedError,
            Status = SyncExceptionStatus.Open,
            CreatedAtUtc = exception.CreatedAtUtc,
            UpdatedAtUtc = exception.CreatedAtUtc,
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<SyncExceptionRecord?> GetExceptionAsync(Guid exceptionId, CancellationToken cancellationToken)
    {
        SyncException? entity = await _dbContext.SyncExceptions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == exceptionId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToRecord(entity);
    }

    /// <inheritdoc />
    public async Task<bool> SetExceptionStatusAsync(
        Guid exceptionId,
        SyncExceptionStatus status,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        SyncException? entity = await _dbContext.SyncExceptions
            .SingleOrDefaultAsync(candidate => candidate.Id == exceptionId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        entity.Status = status;
        entity.UpdatedAtUtc = nowUtc;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncExceptionRecord>> ListUnresolvedExceptionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        List<SyncException> entities = await UnresolvedExceptions(connectionId)
            .AsNoTracking()
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .Take(MaximumListedExceptions)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.ConvertAll(ToRecord);
    }

    /// <inheritdoc />
    public async Task<SyncHistorySummary?> GetSummaryAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        bool? isPaused = await _dbContext.IntegrationConnections
            .AsNoTracking()
            .Where(candidate => candidate.Id == connectionId)
            .Select(candidate => (bool?)candidate.IsPaused)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (isPaused is null)
        {
            return null;
        }

        DateTimeOffset? lastSuccess = await _dbContext.SyncRuns
            .AsNoTracking()
            .Where(candidate => candidate.ConnectionId == connectionId && candidate.LastSuccessAtUtc != null)
            .OrderByDescending(candidate => candidate.LastSuccessAtUtc)
            .Select(candidate => candidate.LastSuccessAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        int backlog = await _dbContext.IntegrationOutbox
            .AsNoTracking()
            .CountAsync(
                candidate => candidate.ConnectionId == connectionId
                    && (candidate.Status == OutboxStatus.Pending || candidate.Status == OutboxStatus.Failed),
                cancellationToken)
            .ConfigureAwait(false);

        int failed = await UnresolvedExceptions(connectionId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SyncHistorySummary(connectionId, isPaused.Value, lastSuccess, backlog, failed);
    }

    private static SyncExceptionRecord ToRecord(SyncException entity)
    {
        return new SyncExceptionRecord(
            entity.Id,
            entity.SyncRunId,
            entity.AffectedResource,
            entity.RedactedError,
            entity.Status,
            entity.CreatedAtUtc);
    }

    private IQueryable<SyncException> UnresolvedExceptions(Guid connectionId)
    {
        return from exception in _dbContext.SyncExceptions
               join run in _dbContext.SyncRuns on exception.SyncRunId equals run.Id
               where run.ConnectionId == connectionId && exception.Status != SyncExceptionStatus.Resolved
               select exception;
    }
}
