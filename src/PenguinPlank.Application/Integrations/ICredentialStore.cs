using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The indirection to external credentials. It handles credential <b>references</b> only; the
/// secret value is never stored in the database, logs, API responses, or browser storage, and is
/// resolved from the deployment secret store by a later connector (requirement R11 / 3.2, 3.3).
/// </summary>
public interface ICredentialStore
{
    /// <summary>
    /// Associates a credential reference with a connection and marks it active. Replaces any
    /// earlier reference, including a revoked or expired one.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="reference">The secret-store locator.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Success, or a validation failure when the connection does not exist.</returns>
    Task<Result> StashReferenceAsync(
        Guid connectionId,
        CredentialReference reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the connection's credential reference. A revoked, expired, or absent credential
    /// resolves with no reference and the reason, so it stops being used.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resolution, or a validation failure when the connection does not exist.</returns>
    Task<Result<CredentialResolution>> ResolveAsync(
        Guid connectionId,
        CancellationToken cancellationToken);

    /// <summary>Records that the connection's credential was revoked or expired.</summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="state">
    /// <see cref="CredentialState.Revoked"/> or <see cref="CredentialState.Expired"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// Success, or a validation failure when the connection does not exist or
    /// <paramref name="state"/> is <see cref="CredentialState.Active"/>.
    /// </returns>
    Task<Result> RecordCredentialStateAsync(
        Guid connectionId,
        CredentialState state,
        CancellationToken cancellationToken);
}
