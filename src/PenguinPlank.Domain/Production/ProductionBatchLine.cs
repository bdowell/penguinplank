using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Production;

/// <summary>
/// One line of a <see cref="ProductionBatch"/>, representing a tracked unit (or sublot) of
/// work moving through the workflow stages.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). A batch line is the target of the Catalog
/// <c>ProductPiece.ProductionBatchLineId</c> seam: in Phase A that remains a documented
/// nullable scalar on the Catalog side (no FK) so the Catalog entity is not modified; the FK
/// is wired additively when Production is implemented. The line is a mutable aggregate.
/// </remarks>
public class ProductionBatchLine : VersionedEntity
{
    /// <summary>The owning <see cref="ProductionBatch"/>. Required.</summary>
    public Guid ProductionBatchId { get; set; }

    /// <summary>The planned quantity for this line (whole units).</summary>
    public int PlannedQuantity { get; set; }

    /// <summary>The completed quantity for this line (whole units).</summary>
    public int CompletedQuantity { get; set; }

    /// <summary>The <see cref="WorkflowStage"/> this line currently sits at, if any.</summary>
    public Guid? CurrentStageId { get; set; }

    /// <summary>The disposition of the line (for example, "InProgress", "Completed", "Scrapped").</summary>
    public string? Disposition { get; set; }
}
