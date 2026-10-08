using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Publishing;

/// <summary>
/// An explicit approval of a <see cref="PublishingDraft"/>, recording who approved it and
/// when.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The approver
/// reference is a designed-only scalar seam (no FK in Phase A). Approval is the deliberate
/// gate that keeps publication from happening automatically.
/// </remarks>
public class PublishingApproval : VersionedEntity
{
    /// <summary>The <see cref="PublishingDraft"/> being approved. Required.</summary>
    public Guid PublishingDraftId { get; set; }

    /// <summary>The actor who approved the draft (designed-only scalar seam; no FK in Phase A).</summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>The instant the draft was approved.</summary>
    public DateTimeOffset ApprovedAtUtc { get; set; }

    /// <summary>An optional approval note.</summary>
    public string? Note { get; set; }
}
