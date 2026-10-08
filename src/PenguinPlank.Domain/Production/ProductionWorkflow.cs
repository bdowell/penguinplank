using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Production;

/// <summary>
/// A reusable, ordered template of production stages a batch moves through.
/// </summary>
/// <remarks>
/// Production is a <b>designed-only</b> module in Phase A (design Section 6): the schema is
/// modeled and migrated, but no services, endpoints, or UI exercise it. This POCO carries
/// no business logic — only the schema shape (coding-standards §1, §3). A workflow owns an
/// ordered set of <see cref="WorkflowStage"/> rows. It is a mutable aggregate and derives
/// from <see cref="VersionedEntity"/>.
/// </remarks>
public class ProductionWorkflow : VersionedEntity
{
    /// <summary>The human-readable name of the workflow template.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>An optional description of what the workflow is used for.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the workflow is active and selectable for new batches.</summary>
    public bool ActiveFlag { get; set; } = true;
}
