using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Wholesale;

/// <summary>
/// A reusable contact person, optionally associated with a <see cref="WholesaleAccount"/>.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). It is a mutable aggregate. The optional
/// <see cref="WholesaleAccountId"/> is a designed-only scalar seam (no FK in Phase A) so a
/// contact can exist independently of any company. Contact details are internal fields.
/// </remarks>
public class WholesaleContact : VersionedEntity
{
    /// <summary>The contact's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The optional associated wholesale account (designed-only scalar seam; no FK).</summary>
    public Guid? WholesaleAccountId { get; set; }

    /// <summary>The contact email (an internal field, never exposed publicly).</summary>
    public string? Email { get; set; }

    /// <summary>The contact phone (an internal field, never exposed publicly).</summary>
    public string? Phone { get; set; }

    /// <summary>Whether the contact is active.</summary>
    public bool ActiveFlag { get; set; } = true;
}
