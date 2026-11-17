namespace PenguinPlank.Contracts.Integrations;

/// <summary>
/// The versioned transport shape for a connection's sync history headline figures
/// (requirement R11 / 3.12): pause state, last success, backlog, and failed items.
/// </summary>
public sealed record SyncRunSummaryResponse
{
    /// <summary>The connection.</summary>
    public required Guid ConnectionId { get; init; }

    /// <summary>Whether the connection is paused.</summary>
    public required bool IsPaused { get; init; }

    /// <summary>The most recent successful sync instant, if any.</summary>
    public DateTimeOffset? LastSuccessAtUtc { get; init; }

    /// <summary>Outgoing jobs still pending or awaiting retry.</summary>
    public required int BacklogCount { get; init; }

    /// <summary>Exceptions not yet resolved.</summary>
    public required int FailedItemCount { get; init; }
}
