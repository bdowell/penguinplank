using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Wholesale;

/// <summary>
/// A recorded interaction with a wholesale account or contact: its date, type, and summary.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The account and
/// contact references are designed-only scalar seams (no FK in Phase A). Date fields carry
/// date-only local intent (A8).
/// </remarks>
public class Interaction : VersionedEntity
{
    /// <summary>The wholesale account the interaction relates to, if any (designed-only scalar seam).</summary>
    public Guid? WholesaleAccountId { get; set; }

    /// <summary>The contact the interaction relates to, if any (designed-only scalar seam).</summary>
    public Guid? WholesaleContactId { get; set; }

    /// <summary>The date of the interaction (date-only local intent; see A8).</summary>
    public DateTimeOffset InteractionDate { get; set; }

    /// <summary>The interaction type (for example, "Call", "Email", "Visit").</summary>
    public string InteractionType { get; set; } = string.Empty;

    /// <summary>A summary of what was discussed.</summary>
    public string? Summary { get; set; }

    /// <summary>The person who recorded the interaction (designed-only scalar seam; no FK).</summary>
    public Guid? AssigneeId { get; set; }
}
