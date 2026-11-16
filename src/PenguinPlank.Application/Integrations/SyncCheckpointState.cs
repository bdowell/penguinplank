namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The cursor position recorded for one connection and resource (requirement 3.9).
/// </summary>
/// <param name="ConnectionId">The owning integration connection.</param>
/// <param name="Resource">The resource the cursor tracks (for example, "orders").</param>
/// <param name="Cursor">The opaque cursor last reached, or <see langword="null"/> before the first poll.</param>
/// <param name="LastReconciledAtUtc">The instant the resource was last reconciled, if ever.</param>
public sealed record SyncCheckpointState(
    Guid ConnectionId,
    string Resource,
    string? Cursor,
    DateTimeOffset? LastReconciledAtUtc);
