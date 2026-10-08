namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// The resolution state of a <see cref="SyncException"/> in the actionable exception queue.
/// </summary>
/// <remarks>
/// A permanent mapping or permission error is routed to the exception queue for the Owner to act
/// on (requirement 3.11 / R11). A new exception is <see cref="Open"/> until retried or reconciled.
/// </remarks>
public enum SyncExceptionStatus
{
    /// <summary>Awaiting owner action. The default for a newly queued exception.</summary>
    Open = 0,

    /// <summary>A retry has been requested for the affected item.</summary>
    RetryRequested = 1,

    /// <summary>The exception has been reconciled or dismissed and requires no further action.</summary>
    Resolved = 2,
}
