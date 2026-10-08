using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Infrastructure.Auditing;

/// <summary>
/// An EF Core <see cref="SaveChangesInterceptor"/> that records an <see cref="AuditEntry"/> for
/// every business mutation <b>within the same transaction as the mutation</b> (requirements A2
/// §5.12, A4 §6.7, A7 §9.6; design "Audit interceptor").
/// </summary>
/// <remarks>
/// <para>
/// <b>Same-transaction atomicity.</b> The interceptor runs in <c>SavingChanges</c>/
/// <c>SavingChangesAsync</c>, which fire <em>inside</em> the <c>SaveChanges</c> call, before EF
/// opens its transaction and writes. It inspects the tracked business entities, builds their audit
/// entries, and adds them to the <em>same</em> <see cref="PenguinPlank.Infrastructure.Persistence.PenguinPlankDbContext"/>.
/// EF then persists the mutation rows and the audit rows together in one <c>SaveChanges</c>
/// transaction, so the audit row commits if and only if the mutation commits and rolls back with it
/// — no separate connection, no second transaction, no window where one exists without the other.
/// </para>
/// <para>
/// <b>Actor.</b> The actor comes from the scoped <see cref="IActorContextAccessor"/>, which the API
/// boundary sets per request and the background worker sets to <see cref="ActorContext.SystemWorker"/>
/// (requirement A7 §9.6). Because this interceptor is scoped alongside the context, each save reads
/// the actor established for that scope. If a mutation reaches the database before the boundary set
/// an actor, that is a wiring defect at the boundary and the interceptor throws rather than writing
/// an unattributable trail.
/// </para>
/// <para>
/// <b>Permitted detail only.</b> Each entry's summary records the operation and the changed
/// property <em>names</em> (never values), so no secret or unnecessary personal data enters the
/// trail (requirement A4 §6.7). The construction itself is delegated to the pure
/// <see cref="AuditEntryFactory"/>, keeping the business rule testable without a database.
/// </para>
/// <para>
/// <b>Exclusions (documented).</b> The interceptor never audits:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="AuditEntry"/> itself — auditing the audit rows would recurse endlessly and add no information.</description></item>
///   <item><description><see cref="IdempotencyRecord"/> — an internal replay-protection bookkeeping row, not a business mutation, so auditing it is noise.</description></item>
///   <item><description>ASP.NET Core Identity rows (users, roles, claims, logins, tokens) — these hold password hashes, security stamps, and other secrets/PII; auditing their columns would risk leaking that sensitive data into the trail, which §6.7 forbids. Account administration is audited at the use-case layer with an explicit, redacted summary instead.</description></item>
/// </list>
/// </remarks>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly System.TimeProvider _timeProvider;

    /// <summary>Creates the interceptor over the actor accessor and the injected time source.</summary>
    /// <param name="actorContextAccessor">Supplies the actor for the current scope.</param>
    /// <param name="timeProvider">The injected time source for audit timestamps (coding-standards §2).</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="actorContextAccessor"/> or <paramref name="timeProvider"/> is null.
    /// </exception>
    public AuditSaveChangesInterceptor(
        IActorContextAccessor actorContextAccessor,
        System.TimeProvider timeProvider)
    {
        System.ArgumentNullException.ThrowIfNull(actorContextAccessor);
        System.ArgumentNullException.ThrowIfNull(timeProvider);

        _actorContextAccessor = actorContextAccessor;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        System.ArgumentNullException.ThrowIfNull(eventData);

        AddAuditEntries(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(eventData);

        AddAuditEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Builds the audit entries for the current change set and adds them to the same context so they
    /// save in the same transaction. Idempotent per save: audit rows the interceptor just added are
    /// themselves excluded, so re-running over the change set would not re-audit them.
    /// </summary>
    /// <param name="context">The context being saved; <see langword="null"/> is a no-op.</param>
    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        List<AuditChange> changes = DescribeChanges(context);

        if (changes.Count == 0)
        {
            return;
        }

        ActorContext actor = _actorContextAccessor.Current
            ?? throw new System.InvalidOperationException(
                "A business mutation was saved without an actor context. The API boundary or the "
                + "background worker must set the current ActorContext before saving.");

        System.DateTimeOffset timestamp = _timeProvider.GetUtcNow();

        IReadOnlyList<AuditEntry> entries = AuditEntryFactory.Create(changes, actor, timestamp);

        context.Set<AuditEntry>().AddRange(entries);
    }

    /// <summary>
    /// Translates the tracked, non-excluded, added/modified/deleted business entities into
    /// persistence-agnostic <see cref="AuditChange"/> descriptions (names, not values).
    /// </summary>
    /// <param name="context">The context being saved.</param>
    /// <returns>One description per audited change.</returns>
    private static List<AuditChange> DescribeChanges(DbContext context)
    {
        var changes = new List<AuditChange>();

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (!IsAuditable(entry))
            {
                continue;
            }

            if (!TryMapOperation(entry.State, out AuditOperation operation))
            {
                continue;
            }

            changes.Add(new AuditChange(
                entry.Entity.GetType().Name,
                ResolveEntityId(entry),
                operation,
                operation == AuditOperation.Updated ? ChangedPropertyNames(entry) : null));
        }

        return changes;
    }

    /// <summary>
    /// Decides whether a tracked entry represents an auditable business mutation. Excludes the audit
    /// trail itself, idempotency bookkeeping, and ASP.NET Core Identity rows (see the type remarks).
    /// </summary>
    /// <param name="entry">The tracked entry.</param>
    /// <returns><see langword="true"/> when the entry should be audited.</returns>
    private static bool IsAuditable(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            return false;
        }

        object entity = entry.Entity;

        // Never audit the audit trail itself (would recurse) or the idempotency bookkeeping row.
        if (entity is AuditEntry or IdempotencyRecord)
        {
            return false;
        }

        // Never audit ASP.NET Core Identity rows: they carry secrets/PII (password hashes,
        // security stamps) that must not reach the trail (requirement A4 §6.7).
        if (IsIdentityType(entity.GetType()))
        {
            return false;
        }

        return true;
    }

    private static bool IsIdentityType(System.Type type)
    {
        // An ASP.NET Core Identity row is either a framework type in the Identity namespace or an
        // application subclass of one (for example AppUser : IdentityUser&lt;Guid&gt;, whose own
        // namespace is this project's, not the framework's). Walk the base chain so a derived
        // Identity type is excluded too — these rows carry secrets/PII and are audited explicitly at
        // the use-case layer instead (requirement A4 §6.7).
        for (System.Type? current = type; current is not null; current = current.BaseType)
        {
            string? clrNamespace = current.Namespace;
            if (clrNamespace is not null
                && clrNamespace.StartsWith("Microsoft.AspNetCore.Identity", System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryMapOperation(EntityState state, out AuditOperation operation)
    {
        switch (state)
        {
            case EntityState.Added:
                operation = AuditOperation.Created;
                return true;
            case EntityState.Modified:
                operation = AuditOperation.Updated;
                return true;
            case EntityState.Deleted:
                operation = AuditOperation.Deleted;
                return true;
            default:
                operation = default;
                return false;
        }
    }

    /// <summary>
    /// Reads the business entity's <see cref="Entity.Id"/> when available, otherwise falls back to
    /// the EF primary-key value so composite-key join entities are still described.
    /// </summary>
    /// <param name="entry">The tracked entry.</param>
    /// <returns>The entity identifier, or <see cref="System.Guid.Empty"/> when no GUID key exists.</returns>
    private static System.Guid ResolveEntityId(EntityEntry entry)
    {
        if (entry.Entity is Entity domainEntity)
        {
            return domainEntity.Id;
        }

        object? keyValue = entry.Metadata.FindPrimaryKey()?.Properties
            .Select(property => entry.Property(property.Name).CurrentValue)
            .FirstOrDefault(value => value is System.Guid);

        return keyValue is System.Guid id ? id : System.Guid.Empty;
    }

    /// <summary>
    /// Collects the names (never the values) of the properties that EF reports as modified.
    /// </summary>
    /// <param name="entry">The tracked, modified entry.</param>
    /// <returns>The changed property names.</returns>
    private static IEnumerable<string> ChangedPropertyNames(EntityEntry entry)
    {
        return entry.Properties
            .Where(property => property.IsModified)
            .Select(property => property.Metadata.Name);
    }
}
