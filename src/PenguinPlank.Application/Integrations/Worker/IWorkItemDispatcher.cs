namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>
/// The boundary through which a leased item reaches a provider connector (requirement 3.16).
/// Provider-specific payload handling lives behind this interface, never in the Domain layer.
/// </summary>
public interface IWorkItemDispatcher
{
    /// <summary>Handles one leased item.</summary>
    /// <param name="item">The leased item.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The outcome the worker records for the item.</returns>
    Task<DispatchOutcome> DispatchAsync(LeasedWorkItem item, CancellationToken cancellationToken);
}
