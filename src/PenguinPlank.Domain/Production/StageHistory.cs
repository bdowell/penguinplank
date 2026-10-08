using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Production;

/// <summary>
/// An append-only record of a single stage transition for a <see cref="ProductionBatchLine"/>.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). Stage history is <b>append-only</b>: rows are
/// inserted when a line enters/leaves a stage and are never mutated afterward, so this type
/// derives from <see cref="Entity"/> rather than <see cref="VersionedEntity"/> and carries no
/// concurrency token. Its parent foreign keys use the model-wide no-cascade-delete default so
/// production history is never destroyed by deleting a batch line.
/// </remarks>
public class StageHistory : Entity
{
    /// <summary>The <see cref="ProductionBatchLine"/> this transition belongs to. Required.</summary>
    public Guid ProductionBatchLineId { get; set; }

    /// <summary>The <see cref="WorkflowStage"/> the line transitioned into. Required.</summary>
    public Guid WorkflowStageId { get; set; }

    /// <summary>The instant the line entered the stage.</summary>
    public DateTimeOffset EnteredAtUtc { get; set; }

    /// <summary>The instant the line left the stage, if it has.</summary>
    public DateTimeOffset? ExitedAtUtc { get; set; }

    /// <summary>An optional note describing the disposition recorded at the transition.</summary>
    public string? Disposition { get; set; }
}
