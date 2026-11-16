namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Pure rule deciding whether new outgoing work may start for a connection. Pausing stops new
/// outgoing work while receipts, mappings, and history stay untouched (requirement 3.13).
/// </summary>
public static class OutgoingWorkPolicy
{
    /// <summary>Decides whether new outgoing work may start.</summary>
    /// <param name="enabled">Whether the connection is enabled (connectors are disabled in Phase A).</param>
    /// <param name="isPaused">Whether the Owner has paused the connection.</param>
    /// <returns><see langword="true"/> only when the connection is enabled and not paused.</returns>
    public static bool CanStartOutgoingWork(bool enabled, bool isPaused)
    {
        return enabled && !isPaused;
    }
}
