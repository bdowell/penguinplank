namespace PenguinPlank.Application.Integrations;

/// <summary>An action an Owner can take on a queued sync exception (requirement 3.12).</summary>
public enum SyncExceptionAction
{
    /// <summary>Request a retry of the affected item.</summary>
    Retry = 0,

    /// <summary>Reconcile the affected item and close the exception.</summary>
    Reconcile = 1,
}
