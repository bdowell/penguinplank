namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>An inbox or outbox item leased to one worker instance.</summary>
/// <param name="Kind">The queue the item belongs to.</param>
/// <param name="Id">The item identifier.</param>
/// <param name="ConnectionId">The owning integration connection.</param>
/// <param name="PayloadReference">The reference to the stored payload.</param>
/// <param name="AttemptNumber">The one-based attempt this lease represents. Recorded when the lease is taken.</param>
public sealed record LeasedWorkItem(
    WorkItemKind Kind,
    Guid Id,
    Guid ConnectionId,
    string PayloadReference,
    int AttemptNumber);
