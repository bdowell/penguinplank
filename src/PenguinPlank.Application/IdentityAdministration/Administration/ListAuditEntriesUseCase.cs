using PenguinPlank.Application.Common;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// Lists audit-trail entries for the Owner-only <c>/audit</c> endpoint (requirement A2 §5.12; A4
/// §6.1, §6.7).
/// </summary>
/// <remarks>
/// A thin pass-through over <see cref="IAuditReader.ListAsync"/>, returning a page of role-safe
/// <see cref="AuditEntryView"/> rows that carry only permitted summaries (never values or secrets).
/// Authorization is enforced at the API boundary (Owner-only, because the trail is sensitive).
/// </remarks>
public sealed class ListAuditEntriesUseCase
{
    private readonly IAuditReader _auditReader;

    /// <summary>Creates the use case with its injected boundary.</summary>
    /// <param name="auditReader">The audit-trail read boundary.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="auditReader"/> is <see langword="null"/>.</exception>
    public ListAuditEntriesUseCase(IAuditReader auditReader)
    {
        ArgumentNullException.ThrowIfNull(auditReader);

        _auditReader = auditReader;
    }

    /// <summary>Lists a page of audit entries matching the query.</summary>
    /// <param name="query">The filter and requested page.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A page of role-safe audit views with the total matching count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    public Task<Page<AuditEntryView>> ExecuteAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _auditReader.ListAsync(query, cancellationToken);
    }
}
