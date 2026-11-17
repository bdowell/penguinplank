using PenguinPlank.Application.Integrations;
using PenguinPlank.Contracts.Integrations;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps between the Integration Contracts DTOs and the Application integration request/read-model
/// shapes at the API boundary (requirements R11 / 3.12, 3.13).
/// </summary>
/// <remarks>
/// A thin, pure boundary translation (coding-standards §3) with no I/O. No mapping here ever reads
/// a credential reference: <see cref="IntegrationConnectionSummary"/> does not carry one, so no
/// response can expose it.
/// </remarks>
public static class IntegrationContractMapper
{
    /// <summary>Parses a connection registration contract into the Application registration.</summary>
    /// <param name="body">The request body.</param>
    /// <returns>The Application registration.</returns>
    /// <exception cref="CatalogContractFormatException">Thrown when the sync direction names no known direction.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="body"/> is <see langword="null"/>.</exception>
    public static ConnectionRegistration ToRegistration(RegisterConnectionContract body)
    {
        System.ArgumentNullException.ThrowIfNull(body);

        if (!System.Enum.TryParse(body.SyncDirection, ignoreCase: true, out SyncDirection direction)
            || !System.Enum.IsDefined(direction))
        {
            throw new CatalogContractFormatException(
                "SyncDirection must be one of: None, Inbound, Outbound, Bidirectional.");
        }

        return new ConnectionRegistration(
            body.Platform,
            body.Account,
            body.Scopes,
            body.ApiVersion,
            body.EnabledCapabilities,
            direction);
    }

    /// <summary>Parses a mapping contract into the Application mapping request.</summary>
    /// <param name="body">The request body.</param>
    /// <returns>The Application mapping request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="body"/> is <see langword="null"/>.</exception>
    public static EntityMappingRequest ToRequest(CreateMappingContract body)
    {
        System.ArgumentNullException.ThrowIfNull(body);
        return new EntityMappingRequest(
            body.ConnectionId,
            body.Platform,
            body.Account,
            body.ExternalId,
            body.OwnerType,
            body.InternalId);
    }

    /// <summary>Projects a connection summary onto its credential-free response.</summary>
    /// <param name="summary">The Application read model.</param>
    /// <returns>The transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="summary"/> is <see langword="null"/>.</exception>
    public static ConnectionResponse ToResponse(IntegrationConnectionSummary summary)
    {
        System.ArgumentNullException.ThrowIfNull(summary);
        return new ConnectionResponse
        {
            Id = summary.Id,
            Platform = summary.Platform,
            Account = summary.Account,
            Scopes = summary.Scopes,
            ApiVersion = summary.ApiVersion,
            EnabledCapabilities = summary.EnabledCapabilities,
            SyncDirection = summary.SyncDirection.ToString(),
            Health = summary.Health.ToString(),
            Enabled = summary.Enabled,
            HasCredential = summary.HasCredential,
            CredentialState = summary.CredentialState.ToString(),
            CredentialStateChangedAtUtc = summary.CredentialStateChangedAtUtc,
        };
    }

    /// <summary>Projects a stored mapping onto its response.</summary>
    /// <param name="record">The Application read model.</param>
    /// <returns>The transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null"/>.</exception>
    public static MappingResponse ToResponse(EntityMappingRecord record)
    {
        System.ArgumentNullException.ThrowIfNull(record);
        return new MappingResponse
        {
            Id = record.Id,
            ConnectionId = record.ConnectionId,
            Platform = record.Platform,
            Account = record.Account,
            ExternalId = record.ExternalId,
            OwnerType = record.OwnerType,
            InternalId = record.InternalId,
        };
    }

    /// <summary>Projects the sync-history headline figures onto their response.</summary>
    /// <param name="summary">The Application read model.</param>
    /// <returns>The transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="summary"/> is <see langword="null"/>.</exception>
    public static SyncRunSummaryResponse ToResponse(SyncHistorySummary summary)
    {
        System.ArgumentNullException.ThrowIfNull(summary);
        return new SyncRunSummaryResponse
        {
            ConnectionId = summary.ConnectionId,
            IsPaused = summary.IsPaused,
            LastSuccessAtUtc = summary.LastSuccessAtUtc,
            BacklogCount = summary.BacklogCount,
            FailedItemCount = summary.FailedItemCount,
        };
    }

    /// <summary>Projects a queued exception with its available actions onto its response.</summary>
    /// <param name="view">The Application read model.</param>
    /// <returns>The transport response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static SyncExceptionResponse ToResponse(SyncExceptionView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);
        SyncExceptionRecord record = view.Exception;
        return new SyncExceptionResponse
        {
            Id = record.Id,
            SyncRunId = record.SyncRunId,
            AffectedResource = record.AffectedResource,
            RedactedError = record.RedactedError,
            Status = record.Status.ToString(),
            CreatedAtUtc = record.CreatedAtUtc,
            AvailableActions = [.. view.AvailableActions.Select(action => action.ToString())],
        };
    }
}
