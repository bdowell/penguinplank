namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// The processing status of an <see cref="IntegrationOutbox"/> job.
/// </summary>
/// <remarks>
/// An outbox job is created in the same transaction as the business change that requires it
/// (requirement 3.14 / R11) and dispatched asynchronously by the worker. Connectors are disabled
/// in Phase A, so a job is never dispatched to a real provider; it simply remains
/// <see cref="Pending"/>.
/// </remarks>
public enum OutboxStatus
{
    /// <summary>Enqueued and awaiting dispatch. The default for a new job.</summary>
    Pending = 0,

    /// <summary>Currently leased by a worker for dispatch.</summary>
    Dispatching = 1,

    /// <summary>Successfully dispatched to the external platform.</summary>
    Dispatched = 2,

    /// <summary>Dispatch failed and the job is awaiting retry.</summary>
    Failed = 3,
}
