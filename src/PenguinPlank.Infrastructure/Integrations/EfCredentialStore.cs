using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="ICredentialStore"/>. It persists only the credential
/// reference and its state; no secret value is stored, and a revoked or expired credential
/// resolves with no reference (requirement R11 / 3.2, 3.3).
/// </summary>
public sealed class EfCredentialStore : ICredentialStore
{
    private const string MissingConnectionMessage = "The integration connection does not exist.";

    private readonly PenguinPlankDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the store over the database context and time source.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    /// <param name="timeProvider">The injected time source for record timestamps.</param>
    public EfCredentialStore(PenguinPlankDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<Result> StashReferenceAsync(
        Guid connectionId,
        CredentialReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        IntegrationConnection? connection = await FindAsync(connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result.Failure(ErrorCode.Validation, MissingConnectionMessage);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        connection.CredentialReference = reference.Value;

        if (connection.CredentialState != CredentialState.Active)
        {
            connection.CredentialState = CredentialState.Active;
            connection.CredentialStateChangedAtUtc = now;
        }

        connection.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<CredentialResolution>> ResolveAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        IntegrationConnection? connection = await _dbContext.IntegrationConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result.Failure<CredentialResolution>(ErrorCode.Validation, MissingConnectionMessage);
        }

        CredentialUseDecision decision = CredentialUsePolicy.Decide(
            connection.CredentialReference,
            connection.CredentialState);

        if (decision != CredentialUseDecision.Usable)
        {
            return Result.Success(new CredentialResolution(decision, null));
        }

        Result<CredentialReference> reference = CredentialReference.Create(connection.CredentialReference);

        // A stored reference that no longer validates is a corrupt record, not a usable credential.
        return reference.IsSuccess
            ? Result.Success(new CredentialResolution(decision, reference.Value))
            : Result.Success(new CredentialResolution(CredentialUseDecision.NoCredential, null));
    }

    /// <inheritdoc />
    public async Task<Result> RecordCredentialStateAsync(
        Guid connectionId,
        CredentialState state,
        CancellationToken cancellationToken)
    {
        if (state == CredentialState.Active)
        {
            return Result.Failure(
                ErrorCode.Validation,
                "Only revocation or expiration can be recorded; stash a new reference to reactivate.");
        }

        IntegrationConnection? connection = await FindAsync(connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result.Failure(ErrorCode.Validation, MissingConnectionMessage);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        connection.CredentialState = state;
        connection.CredentialStateChangedAtUtc = now;
        connection.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private Task<IntegrationConnection?> FindAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        return _dbContext.IntegrationConnections
            .SingleOrDefaultAsync(candidate => candidate.Id == connectionId, cancellationToken);
    }
}
