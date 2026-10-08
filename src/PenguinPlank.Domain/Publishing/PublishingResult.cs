using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Publishing;

/// <summary>
/// The remote result of publishing a <see cref="PublishingDraft"/>: the remote identifier,
/// status, and any redacted error.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. In Phase A no
/// connector dispatches a publish, so no result is produced; the schema reserves the shape.
/// </remarks>
public class PublishingResult : VersionedEntity
{
    /// <summary>The <see cref="PublishingDraft"/> this result corresponds to. Required.</summary>
    public Guid PublishingDraftId { get; set; }

    /// <summary>The remote identifier assigned by the destination, if any.</summary>
    public string? RemoteId { get; set; }

    /// <summary>The result status (for example, "Pending", "Published", "Failed").</summary>
    public string? Status { get; set; }

    /// <summary>A redacted error message when publishing failed (never includes secrets).</summary>
    public string? RedactedError { get; set; }

    /// <summary>The instant the publish completed, if it did.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
