using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Production;

/// <summary>
/// One ordered stage within a <see cref="ProductionWorkflow"/> template, with an expected
/// wait duration.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). A stage belongs to exactly one workflow via
/// <see cref="ProductionWorkflowId"/> and carries its order within that workflow. The
/// expected wait is modeled as whole hours so later phases can compute target dates; no
/// business logic lives here.
/// </remarks>
public class WorkflowStage : Entity
{
    /// <summary>The owning <see cref="ProductionWorkflow"/>. Required.</summary>
    public Guid ProductionWorkflowId { get; set; }

    /// <summary>The stage name (for example, "Glue-up", "Sanding", "Finishing").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The zero-based position of this stage within its workflow.</summary>
    public int SequenceOrder { get; set; }

    /// <summary>The expected wait duration before the stage completes, in whole hours.</summary>
    public int ExpectedWaitHours { get; set; }
}
