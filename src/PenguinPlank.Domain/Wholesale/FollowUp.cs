using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Wholesale;

/// <summary>
/// A follow-up task against a wholesale account or contact: its assignee, due date, and
/// status.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The account and
/// contact references are designed-only scalar seams (no FK in Phase A). The design calls for
/// a follow-up index on (assignee, status, date); that index is declared in the
/// configuration. Date fields carry date-only local intent (A8).
/// </remarks>
public class FollowUp : VersionedEntity
{
    /// <summary>The wholesale account the follow-up relates to, if any (designed-only scalar seam).</summary>
    public Guid? WholesaleAccountId { get; set; }

    /// <summary>The contact the follow-up relates to, if any (designed-only scalar seam).</summary>
    public Guid? WholesaleContactId { get; set; }

    /// <summary>The person responsible for the follow-up (designed-only scalar seam; no FK).</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>The follow-up due date (date-only local intent; see A8).</summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>The follow-up status (for example, "Open", "Done", "Cancelled").</summary>
    public string? Status { get; set; }

    /// <summary>An optional note describing the follow-up.</summary>
    public string? Note { get; set; }
}
