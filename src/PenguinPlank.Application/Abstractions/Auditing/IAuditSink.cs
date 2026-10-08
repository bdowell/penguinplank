using PenguinPlank.Domain.Auditing;

namespace PenguinPlank.Application.Abstractions.Auditing;

/// <summary>
/// The persistence boundary for the audit trail: records an <see cref="AuditEntry"/> describing a
/// business mutation (design "Audit interceptor"; requirements A2 §5.12, A4 §6.7).
/// </summary>
/// <remarks>
/// <para>
/// The automatic trail is produced by the EF <c>SaveChanges</c> interceptor, which adds audit rows
/// to the same <c>DbContext</c> inside the same transaction as the mutation (so the audit row
/// commits if and only if the mutation commits). <see cref="IAuditSink"/> exists for the cases the
/// design calls out as an <em>explicit use-case call for domain-significant summaries</em>: a use
/// case that wants to record a richer, hand-written permitted summary alongside the automatic
/// per-entity entries. Implementations enlist the entry in the ambient unit of work rather than
/// opening their own transaction, preserving the same-transaction guarantee.
/// </para>
/// <para>
/// The method takes and propagates a <see cref="System.Threading.CancellationToken"/> and returns
/// <see cref="System.Threading.Tasks.Task"/>; expected business failures are not modeled here
/// because recording audit is not a business decision — a persistence failure is an unexpected
/// failure that aborts the transaction (coding-standards §6).
/// </para>
/// </remarks>
public interface IAuditSink
{
    /// <summary>
    /// Enlists an <paramref name="entry"/> to be written with the current unit of work, inside the
    /// same transaction as the mutation it describes.
    /// </summary>
    /// <param name="entry">The audit entry to record.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the entry has been enlisted.</returns>
    System.Threading.Tasks.Task RecordAsync(AuditEntry entry, System.Threading.CancellationToken cancellationToken);
}
