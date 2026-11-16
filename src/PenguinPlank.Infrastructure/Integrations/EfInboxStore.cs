using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// The EF Core implementation of <see cref="IInboxStore"/> over the
/// <c>IntegrationInbox</c> table. The <b>unique index on <c>DeliveryKey</c></b> makes
/// deduplication safe under concurrent deliveries (requirements 3.6, 3.8).
/// </summary>
/// <remarks>
/// A pre-check avoids a needless failing insert in the common duplicate case; the duplicate-key
/// catch handles the race where two deliveries pass the pre-check together. Only a duplicate-key
/// failure on the insert is translated to <see langword="false"/>; every other failure propagates
/// so a delivery is never reported as recorded when the write failed (coding-standards §6).
/// </remarks>
public sealed class EfInboxStore : IInboxStore
{
    // SQL Server duplicate-key error numbers: 2627 (unique constraint) and 2601 (unique index).
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    // Matches the persisted IntegrationInbox.SignatureState column length.
    private const int MaxSignatureStateLength = 64;

    private readonly PenguinPlankDbContext _dbContext;
    private readonly System.TimeProvider _timeProvider;

    /// <summary>Creates the store over the database context and time source.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    /// <param name="timeProvider">The injected time source for timestamps.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="dbContext"/> or <paramref name="timeProvider"/> is null.
    /// </exception>
    public EfInboxStore(PenguinPlankDbContext dbContext, System.TimeProvider timeProvider)
    {
        System.ArgumentNullException.ThrowIfNull(dbContext);
        System.ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<bool> TryRecordAsync(
        InboundDelivery delivery,
        CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(delivery);

        // Fail fast on a boundary mistake rather than as an opaque SQL truncation error.
        if (string.IsNullOrWhiteSpace(delivery.SignatureState)
            || delivery.SignatureState.Length > MaxSignatureStateLength)
        {
            throw new System.ArgumentException(
                $"A signature state must be 1 to {MaxSignatureStateLength} characters.",
                nameof(delivery));
        }

        string key = delivery.DeliveryKey.Value;

        bool exists = await _dbContext.IntegrationInbox
            .AnyAsync(item => item.DeliveryKey == key, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return false;
        }

        System.DateTimeOffset now = _timeProvider.GetUtcNow();

        var item = new IntegrationInbox
        {
            Id = System.Guid.NewGuid(),
            ConnectionId = delivery.ConnectionId,
            DeliveryKey = key,
            PayloadReference = delivery.Payload.Value,
            SignatureState = delivery.SignatureState,
            Status = InboxStatus.Received,
            Attempts = 0,
            ReceivedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _dbContext.IntegrationInbox.Add(item);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // A concurrent delivery with the same key committed first. Detach our failed insert
            // so the context stays usable, and report the duplicate.
            _dbContext.Entry(item).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && (sqlException.Number == UniqueConstraintViolation
                || sqlException.Number == UniqueIndexViolation);
    }
}
