namespace PenguinPlank.Application.Integrations;

/// <summary>
/// An outgoing integration job to be enqueued in the same transaction as the business change
/// that requires it.
/// </summary>
/// <param name="ConnectionId">The owning integration connection.</param>
/// <param name="OperationKey">The unique key of the operation; it is enqueued at most once.</param>
/// <param name="Payload">A reference to the stored job payload.</param>
public sealed record OutboxJob(
    System.Guid ConnectionId,
    OperationKey OperationKey,
    PayloadRef Payload);
