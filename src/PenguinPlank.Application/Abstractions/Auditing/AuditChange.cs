using System.Collections.ObjectModel;

namespace PenguinPlank.Application.Abstractions.Auditing;

/// <summary>
/// A persistence-agnostic description of a single business mutation to one entity: what type it
/// was, which instance, which operation, and the <b>names</b> of the properties that changed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Permitted detail only (requirement A4 §6.7).</b> An <see cref="AuditChange"/> carries only
/// safe metadata — the entity type name, the entity id, the operation, and the set of changed
/// property <em>names</em>. It deliberately carries <b>no property values</b>: recording changed
/// names (for example "SalePrice", "Barcode") describes <em>what</em> changed without ever storing
/// the old or new value, so no secret (password hash, security stamp, credential reference) and no
/// unnecessary personal data can leak into the trail. The caller that builds the change set — the
/// EF <c>SaveChanges</c> interceptor — is responsible for excluding sensitive entity types
/// entirely (see the interceptor's exclusion list).
/// </para>
/// <para>
/// This is an immutable value object with no I/O, so the audit-entry factory that consumes it is a
/// pure function and the audit-coverage property can be asserted over described change sets without
/// a database (coding-standards §1).
/// </para>
/// </remarks>
public sealed class AuditChange
{
    /// <summary>
    /// Creates a described change.
    /// </summary>
    /// <param name="entityType">The simple type name of the affected entity (for example "ProductVariant").</param>
    /// <param name="entityId">The identifier of the affected entity instance.</param>
    /// <param name="operation">The operation performed.</param>
    /// <param name="changedPropertyNames">
    /// The names of the properties that changed. Names only — never values. For a create or delete
    /// this may be empty; for an update it lists the modified columns at a safe granularity.
    /// </param>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="entityType"/> is null or whitespace.</exception>
    public AuditChange(
        string entityType,
        System.Guid entityId,
        AuditOperation operation,
        IEnumerable<string>? changedPropertyNames = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new System.ArgumentException("An audit change requires a non-empty entity type.", nameof(entityType));
        }

        EntityType = entityType;
        EntityId = entityId;
        Operation = operation;

        IReadOnlyList<string> names = changedPropertyNames is null
            ? System.Array.Empty<string>()
            : changedPropertyNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToList();

        ChangedPropertyNames = new ReadOnlyCollection<string>(names.ToList());
    }

    /// <summary>The simple type name of the affected entity.</summary>
    public string EntityType { get; }

    /// <summary>The identifier of the affected entity instance.</summary>
    public System.Guid EntityId { get; }

    /// <summary>The operation performed.</summary>
    public AuditOperation Operation { get; }

    /// <summary>
    /// The names (never values) of the properties that changed, sorted for a stable summary. Empty
    /// for a create or delete.
    /// </summary>
    public IReadOnlyList<string> ChangedPropertyNames { get; }
}
