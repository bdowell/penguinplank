namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Persistence boundary for pausing and resuming a connection (requirement 3.13). Pausing stops
/// new outgoing work only; receipts, mappings, and history are never changed or deleted.
/// </summary>
public interface IConnectionPauseStore
{
    /// <summary>Pauses a connection. Pausing an already paused connection changes nothing.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="false"/> when no connection matches the id.</returns>
    Task<bool> PauseAsync(Guid connectionId, CancellationToken cancellationToken);

    /// <summary>Resumes a connection. Resuming a connection that is not paused changes nothing.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="false"/> when no connection matches the id.</returns>
    Task<bool> ResumeAsync(Guid connectionId, CancellationToken cancellationToken);

    /// <summary>Reads the fields that decide whether outgoing work may start.</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The state, or <see langword="null"/> when no connection matches the id.</returns>
    Task<OutgoingWorkState?> GetOutgoingWorkStateAsync(Guid connectionId, CancellationToken cancellationToken);
}
