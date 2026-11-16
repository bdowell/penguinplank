using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The persistence boundary for platform connection records (requirement R11 / 3.1). Phase A
/// keeps every connector disabled, so the store records connections as disabled and offers no
/// way to enable one.
/// </summary>
public interface IIntegrationConnectionStore
{
    /// <summary>
    /// Records a new connection: disabled, health unknown, with no credential.
    /// </summary>
    /// <param name="connectionId">The identifier for the new connection.</param>
    /// <param name="registration">The connection details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The recorded connection.</returns>
    Task<IntegrationConnectionSummary> RecordAsync(
        Guid connectionId,
        ConnectionRegistration registration,
        CancellationToken cancellationToken);

    /// <summary>Gets a connection, or null when none has the identifier.</summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The connection, or null.</returns>
    Task<IntegrationConnectionSummary?> GetAsync(Guid connectionId, CancellationToken cancellationToken);

    /// <summary>Lists all recorded connections ordered by platform then account.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The connections.</returns>
    Task<IReadOnlyList<IntegrationConnectionSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Records the observed health of a connection.</summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="health">The observed health.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Success, or a validation failure when the connection does not exist.</returns>
    Task<Result> UpdateHealthAsync(
        Guid connectionId,
        PenguinPlank.Domain.Integrations.ConnectionHealth health,
        CancellationToken cancellationToken);
}
