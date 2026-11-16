using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="IConnectionPauseStore"/> (requirement 3.13). It only
/// changes the pause fields of the connection row; receipts, mappings, and history are untouched.
/// </summary>
public sealed class EfConnectionPauseStore : IConnectionPauseStore
{
    private readonly PenguinPlankDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the store over the database context and time source.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    /// <param name="timeProvider">The time source for pause timestamps.</param>
    public EfConnectionPauseStore(PenguinPlankDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<bool> PauseAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        return SetPausedAsync(connectionId, isPaused: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ResumeAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        return SetPausedAsync(connectionId, isPaused: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OutgoingWorkState?> GetOutgoingWorkStateAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.IntegrationConnections
            .AsNoTracking()
            .Where(candidate => candidate.Id == connectionId)
            .Select(candidate => new OutgoingWorkState(candidate.Enabled, candidate.IsPaused))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> SetPausedAsync(Guid connectionId, bool isPaused, CancellationToken cancellationToken)
    {
        IntegrationConnection? connection = await _dbContext.IntegrationConnections
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return false;
        }

        if (connection.IsPaused == isPaused)
        {
            return true;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        connection.IsPaused = isPaused;
        connection.PausedAtUtc = isPaused ? now : null;
        connection.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
