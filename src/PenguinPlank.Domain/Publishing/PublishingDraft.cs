using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Publishing;

/// <summary>
/// A draft of content to be published to an external destination: a content snapshot, the
/// destination, the approved fields/media, and an optional schedule.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6); no services exercise it. It is a mutable
/// aggregate. The source catalog-reference link is a designed-only scalar seam (no FK in Phase
/// A). Nothing publishes automatically — a draft only becomes a result through an explicit
/// approval and dispatch in a later phase.
/// </remarks>
public class PublishingDraft : VersionedEntity
{
    /// <summary>The kind of thing referenced (for example, "Product", "Variant", "Piece", "Event").</summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>The referenced source entity's id (designed-only scalar seam; no FK in Phase A).</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>The destination channel/platform for the draft.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>A snapshot of the content to publish.</summary>
    public string? ContentSnapshot { get; set; }

    /// <summary>The allowlist of approved fields to publish.</summary>
    public string? ApprovedFields { get; set; }

    /// <summary>The allowlist of approved media asset ids to publish.</summary>
    public string? ApprovedMedia { get; set; }

    /// <summary>The scheduled publish instant, if the draft is scheduled.</summary>
    public DateTimeOffset? ScheduledAtUtc { get; set; }
}
