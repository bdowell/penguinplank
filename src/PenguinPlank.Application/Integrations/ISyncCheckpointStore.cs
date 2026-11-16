namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Persistence boundary for sync cursors used by incremental polling, backfill, and periodic
/// reconciliation (requirement 3.9).
/// </summary>
public interface ISyncCheckpointStore
{
    /// <summary>Reads the checkpoint for a connection and resource.</summary>
    /// <param name="connectionId">The owning connection.</param>
    /// <param name="resource">The tracked resource.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The checkpoint, or <see langword="null"/> when none has been recorded.</returns>
    Task<SyncCheckpointState?> GetAsync(Guid connectionId, string resource, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the cursor to <paramref name="newCursor"/> if the stored cursor still equals
    /// <paramref name="expectedCursor"/>. Creates the checkpoint when none exists and
    /// <paramref name="expectedCursor"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="connectionId">The owning connection.</param>
    /// <param name="resource">The tracked resource.</param>
    /// <param name="expectedCursor">The cursor the caller last read, or <see langword="null"/> for none.</param>
    /// <param name="newCursor">The new cursor position.</param>
    /// <param name="nowUtc">The instant recorded as the modification time.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Whether the cursor advanced or was stale.</returns>
    Task<CheckpointAdvanceOutcome> AdvanceAsync(
        Guid connectionId,
        string resource,
        string? expectedCursor,
        string newCursor,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>Records that a resource was reconciled at <paramref name="reconciledAtUtc"/>, creating the checkpoint if needed.</summary>
    /// <param name="connectionId">The owning connection.</param>
    /// <param name="resource">The tracked resource.</param>
    /// <param name="reconciledAtUtc">The reconciliation instant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the checkpoint is saved.</returns>
    Task MarkReconciledAsync(
        Guid connectionId,
        string resource,
        DateTimeOffset reconciledAtUtc,
        CancellationToken cancellationToken);
}
