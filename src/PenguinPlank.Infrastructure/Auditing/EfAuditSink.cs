using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Auditing;

/// <summary>
/// The EF Core <see cref="IAuditSink"/>: enlists a hand-written <see cref="AuditEntry"/> in the
/// scoped <see cref="PenguinPlankDbContext"/> so it is written in the same <c>SaveChanges</c>
/// transaction as the mutation it describes (design "Audit interceptor").
/// </summary>
/// <remarks>
/// <para>
/// This sink does <b>not</b> call <c>SaveChanges</c>: it adds the entry to the change tracker and
/// returns, leaving the use case's own <c>SaveChanges</c> to commit the entry atomically with the
/// business mutation. That preserves the same-transaction guarantee for the automatic interceptor
/// trail and the explicit domain-significant summaries alike (requirements A2 §5.12, A4 §6.7). The
/// sink is registered <b>scoped</b> because it captures the scoped context (requirement A2 §5.14).
/// </para>
/// </remarks>
public sealed class EfAuditSink : IAuditSink
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the sink over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped application database context.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="dbContext"/> is null.</exception>
    public EfAuditSink(PenguinPlankDbContext dbContext)
    {
        System.ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        _dbContext.Set<AuditEntry>().Add(entry);

        return Task.CompletedTask;
    }
}
