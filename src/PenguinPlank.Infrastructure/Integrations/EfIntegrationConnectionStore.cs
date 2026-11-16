using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="IIntegrationConnectionStore"/>. New connections are
/// always recorded disabled, because Phase A connectors are disabled (requirement R11 / 3.1).
/// </summary>
public sealed class EfIntegrationConnectionStore : IIntegrationConnectionStore
{
    private readonly PenguinPlankDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the store over the database context and time source.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    /// <param name="timeProvider">The injected time source for record timestamps.</param>
    public EfIntegrationConnectionStore(PenguinPlankDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<IntegrationConnectionSummary> RecordAsync(
        Guid connectionId,
        ConnectionRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var connection = new IntegrationConnection
        {
            Id = connectionId,
            Platform = registration.Platform,
            Account = registration.Account,
            Scopes = registration.Scopes,
            ApiVersion = registration.ApiVersion,
            EnabledCapabilities = registration.EnabledCapabilities,
            SyncDirection = registration.SyncDirection,
            Health = ConnectionHealth.Unknown,
            Enabled = false,
            CredentialReference = null,
            CredentialState = CredentialState.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _dbContext.IntegrationConnections.Add(connection);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToSummary(connection);
    }

    /// <inheritdoc />
    public async Task<IntegrationConnectionSummary?> GetAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        IntegrationConnection? connection = await _dbContext.IntegrationConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken)
            .ConfigureAwait(false);

        return connection is null ? null : ToSummary(connection);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntegrationConnectionSummary>> ListAsync(
        CancellationToken cancellationToken)
    {
        List<IntegrationConnection> connections = await _dbContext.IntegrationConnections
            .AsNoTracking()
            .OrderBy(connection => connection.Platform)
            .ThenBy(connection => connection.Account)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return connections.ConvertAll(ToSummary);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateHealthAsync(
        Guid connectionId,
        ConnectionHealth health,
        CancellationToken cancellationToken)
    {
        IntegrationConnection? connection = await _dbContext.IntegrationConnections
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result.Failure(ErrorCode.Validation, "The integration connection does not exist.");
        }

        connection.Health = health;
        connection.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static IntegrationConnectionSummary ToSummary(IntegrationConnection connection)
    {
        return new IntegrationConnectionSummary(
            connection.Id,
            connection.Platform,
            connection.Account,
            connection.Scopes,
            connection.ApiVersion,
            connection.EnabledCapabilities,
            connection.SyncDirection,
            connection.Health,
            connection.Enabled,
            !string.IsNullOrWhiteSpace(connection.CredentialReference),
            connection.CredentialState,
            connection.CredentialStateChangedAtUtc);
    }
}
