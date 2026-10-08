using PenguinPlank.Application.Common;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// The narrow read boundary for the audit trail (requirement A2 §5.12; A4 §6.7).
/// </summary>
/// <remarks>
/// A focused, responsibility-named interface defined in Application and implemented in
/// Infrastructure over EF Core (coding-standards §2, §3). The trail is append-only, so this
/// boundary is read-only: audit rows are written by the <c>SaveChanges</c> interceptor and the
/// explicit <c>IAuditSink</c>, never by a reader. It materializes a parameterized, paged query into
/// a <see cref="Page{T}"/> of role-safe <see cref="AuditEntryView"/> rows carrying the total
/// matching count (requirement A4 §6.1); no EF entity or <c>IQueryable</c> crosses the boundary.
/// It takes and propagates a <see cref="CancellationToken"/>. The <c>/audit</c> endpoint is gated
/// Owner-only because the trail is sensitive.
/// </remarks>
public interface IAuditReader
{
    /// <summary>
    /// Reads a page of audit entries matching the query, most-recent-first.
    /// </summary>
    /// <param name="query">The filter and requested page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A page of role-safe audit views with the total matching count.</returns>
    Task<Page<AuditEntryView>> ListAsync(AuditQuery query, CancellationToken cancellationToken);
}
