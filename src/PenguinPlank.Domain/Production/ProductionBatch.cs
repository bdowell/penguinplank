using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Production;

/// <summary>
/// A planned run of production work against a <see cref="ProductionWorkflow"/> for a given
/// variant, tracking planned/completed quantities, priority, target date, and an optional
/// sublot parent.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6); no services exercise it. The batch is a
/// mutable aggregate (<see cref="VersionedEntity"/>). <see cref="WorkflowSnapshot"/> captures
/// the workflow definition in effect when the batch started so later stage edits do not
/// rewrite history. Quantities are whole units. <see cref="ParentBatchId"/> is a self-link
/// reserving the sublot relationship and carries no FK constraint in Phase A.
/// </remarks>
public class ProductionBatch : VersionedEntity
{
    /// <summary>The <see cref="ProductionWorkflow"/> this batch follows. Required.</summary>
    public Guid ProductionWorkflowId { get; set; }

    /// <summary>The variant being produced (designed-only scalar link to Catalog ProductVariant).</summary>
    public Guid VariantId { get; set; }

    /// <summary>The number of units planned for this batch (whole units).</summary>
    public int PlannedQuantity { get; set; }

    /// <summary>The number of units completed so far (whole units).</summary>
    public int CompletedQuantity { get; set; }

    /// <summary>The batch priority (higher runs sooner); a simple ordinal in Phase A.</summary>
    public int Priority { get; set; }

    /// <summary>The target completion date (date-only intent; see A8 time handling).</summary>
    public DateTimeOffset? TargetDate { get; set; }

    /// <summary>The current batch status (a free-form operational status label in Phase A).</summary>
    public string? Status { get; set; }

    /// <summary>
    /// A snapshot of the workflow definition in effect when the batch started, preserved so
    /// later workflow edits never rewrite in-flight batch history.
    /// </summary>
    public string? WorkflowSnapshot { get; set; }

    /// <summary>
    /// The optional parent batch when this batch is a sublot. A plain nullable self-link GUID
    /// with no FK constraint in Phase A, reserving the sublot seam additively.
    /// </summary>
    public Guid? ParentBatchId { get; set; }
}
