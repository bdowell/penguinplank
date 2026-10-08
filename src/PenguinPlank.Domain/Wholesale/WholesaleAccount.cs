using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Wholesale;

/// <summary>
/// A wholesale account: a company and its relationship stage.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6); no services exercise it. It is a mutable
/// aggregate. Internal notes are stored separately from any public-safe content.
/// </remarks>
public class WholesaleAccount : VersionedEntity
{
    /// <summary>The company name.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>The relationship stage (for example, "Prospect", "Active", "Dormant").</summary>
    public string? Stage { get; set; }

    /// <summary>Internal-only notes about the account (never exposed publicly).</summary>
    public string? InternalNotes { get; set; }

    /// <summary>Whether the account is active.</summary>
    public bool ActiveFlag { get; set; } = true;
}
