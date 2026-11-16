namespace PenguinPlank.Application.Integrations;

/// <summary>The result of staging an outbox job for the current transaction.</summary>
public enum OutboxEnqueueOutcome
{
    /// <summary>The job was staged and will commit or roll back with the business change.</summary>
    Staged,

    /// <summary>A job with the same operation key already exists; nothing was staged.</summary>
    AlreadyEnqueued,
}
