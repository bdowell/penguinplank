using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Marketing;

/// <summary>
/// A marketing/listing task against a product, variant, or piece: its channel, type,
/// assignee, due date, state, and completion.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6); no services exercise it. It is a mutable
/// aggregate. The catalog-reference link is a designed-only scalar seam (its kind recorded by
/// <see cref="ReferenceType"/>) with no FK in Phase A. Date fields carry date-only local
/// intent (A8).
/// </remarks>
public class ListingTask : VersionedEntity
{
    /// <summary>The kind of thing referenced (for example, "Product", "Variant", "Piece").</summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>The referenced catalog entity's id (designed-only scalar seam; no FK in Phase A).</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>The channel the task targets (for example, "Shopify", "Etsy", "Instagram").</summary>
    public string? Channel { get; set; }

    /// <summary>The task type (for example, "Photograph", "Write copy", "Publish").</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>The assignee responsible for the task (designed-only scalar seam; no FK in Phase A).</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>The task due date (date-only local intent; see A8).</summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>The task state (for example, "Open", "InProgress", "Done").</summary>
    public string? State { get; set; }

    /// <summary>The instant the task was completed, if it has been.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
