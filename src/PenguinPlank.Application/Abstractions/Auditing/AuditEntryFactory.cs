using PenguinPlank.Domain.Auditing;

namespace PenguinPlank.Application.Abstractions.Auditing;

/// <summary>
/// Builds <see cref="AuditEntry"/> records from a described change set, the responsible actor, and
/// a timestamp. This is a <b>pure</b> function: it performs no I/O, reads no clock, and touches no
/// database, so the "every business mutation is audited" property can be asserted over any change
/// set without starting the application (coding-standards §1; design Property 15).
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="AuditChange"/> produces exactly one <see cref="AuditEntry"/>, so the mapping is
/// one-to-one and total: no described business mutation can go unaudited. The entry records the
/// actor id (an authenticated staff user or <see cref="ActorContext.SystemWorkerId"/> for the
/// background worker — requirement A7 §9.6), the action, the affected entity type and id, the
/// supplied timestamp, and a permitted change summary (requirements A2 §5.12, A4 §6.7).
/// </para>
/// <para>
/// <b>What the summary records and why it is safe.</b> The summary is a short, human-readable
/// string composed only of the operation and — for updates — the sorted set of changed property
/// <em>names</em>. It never contains property <em>values</em>, so it cannot carry a secret or
/// unnecessary personal data. The caller excludes sensitive entity types from the change set
/// before it reaches this factory.
/// </para>
/// </remarks>
public static class AuditEntryFactory
{
    /// <summary>
    /// Produces one <see cref="AuditEntry"/> per described change.
    /// </summary>
    /// <param name="changes">The described business mutations in this unit of work.</param>
    /// <param name="actor">The actor responsible for the mutations.</param>
    /// <param name="timestamp">The instant to record (supplied from an injected <c>TimeProvider</c>).</param>
    /// <returns>An audit entry for every change, in input order.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="changes"/> or <paramref name="actor"/> is <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<AuditEntry> Create(
        IEnumerable<AuditChange> changes,
        ActorContext actor,
        System.DateTimeOffset timestamp)
    {
        System.ArgumentNullException.ThrowIfNull(changes);
        System.ArgumentNullException.ThrowIfNull(actor);

        return changes.Select(change => CreateEntry(change, actor, timestamp)).ToList();
    }

    /// <summary>
    /// Produces a single <see cref="AuditEntry"/> for one described change.
    /// </summary>
    /// <param name="change">The described business mutation.</param>
    /// <param name="actor">The actor responsible for the mutation.</param>
    /// <param name="timestamp">The instant to record.</param>
    /// <returns>The audit entry describing the change.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="change"/> or <paramref name="actor"/> is <see langword="null"/>.
    /// </exception>
    public static AuditEntry CreateEntry(
        AuditChange change,
        ActorContext actor,
        System.DateTimeOffset timestamp)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        System.ArgumentNullException.ThrowIfNull(actor);

        return new AuditEntry
        {
            ActorId = actor.UserId,
            Action = BuildAction(change),
            EntityType = change.EntityType,
            EntityId = change.EntityId,
            Timestamp = timestamp,
            PermittedChangeSummary = BuildSummary(change),
        };
    }

    private static string BuildAction(AuditChange change)
    {
        // A stable, machine-readable action name such as "ProductVariantUpdated".
        return change.EntityType + change.Operation;
    }

    private static string BuildSummary(AuditChange change)
    {
        // Permitted detail only: operation plus changed property NAMES — never values
        // (requirement A4 §6.7). For a create/delete there are no changed-name details.
        if (change.Operation == AuditOperation.Updated && change.ChangedPropertyNames.Count > 0)
        {
            return $"{change.Operation}: {string.Join(", ", change.ChangedPropertyNames)}";
        }

        return change.Operation.ToString();
    }
}
