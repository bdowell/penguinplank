namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>
/// The Phase A dispatcher. All concrete connectors are disabled (requirement 3.17), so it never
/// sends anything and always reports <see cref="DispatchOutcome.ConnectorDisabled"/>.
/// </summary>
public sealed class DisabledConnectorDispatcher : IWorkItemDispatcher
{
    /// <inheritdoc />
    public Task<DispatchOutcome> DispatchAsync(LeasedWorkItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        return Task.FromResult(DispatchOutcome.ConnectorDisabled);
    }
}
