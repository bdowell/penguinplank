using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Common;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Auditing;

/// <summary>
/// The EF Core implementation of <see cref="IAuditReader"/> over the
/// <see cref="PenguinPlankDbContext"/>. It reads a parameterized, paged, most-recent-first slice of
/// the append-only audit trail as flat Application read models (requirement A2 §5.12; A4 §6.1,
/// §6.7).
/// </summary>
/// <remarks>
/// Every query is read-only (<see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>)
/// and materializes into the Application <see cref="AuditEntryView"/> — no <c>IQueryable</c>,
/// <see cref="PenguinPlankDbContext"/>, or EF entity leaves the boundary (coding-standards §2, §3).
/// It applies the normalized <see cref="PageRequest"/> (default size 50, clamped at 200) with a
/// stable timestamp-then-identifier sort, and returns a <see cref="Page{T}"/> carrying the total
/// matching count. The reader captures the scoped context and is registered scoped.
/// </remarks>
public sealed class EfAuditReader : IAuditReader
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the reader over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfAuditReader(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Page<AuditEntryView>> ListAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        PageRequest page = query.Page.Normalize();

        IQueryable<AuditEntry> filtered = _dbContext.AuditEntries.AsNoTracking();

        if (query.ActorId is Guid actorId)
        {
            filtered = filtered.Where(entry => entry.ActorId == actorId);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            string entityType = query.EntityType.Trim();
            filtered = filtered.Where(entry => entry.EntityType == entityType);
        }

        if (query.EntityId is Guid entityId)
        {
            filtered = filtered.Where(entry => entry.EntityId == entityId);
        }

        long totalCount = await filtered.LongCountAsync(cancellationToken).ConfigureAwait(false);

        List<AuditEntryView> items = await filtered
            .OrderByDescending(entry => entry.Timestamp)
            .ThenBy(entry => entry.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(entry => new AuditEntryView
            {
                AuditEntryId = entry.Id,
                ActorId = entry.ActorId,
                Action = entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                Timestamp = entry.Timestamp,
                PermittedChangeSummary = entry.PermittedChangeSummary,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Page.Create(items, totalCount, page);
    }
}
