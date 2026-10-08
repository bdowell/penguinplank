using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Markets;

/// <summary>
/// A reusable template describing what to pack for an event.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6). A template owns an ordered set of
/// <see cref="PackingTemplateItem"/> rows. It is a mutable aggregate.
/// </remarks>
public class PackingTemplate : VersionedEntity
{
    /// <summary>The template name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>An optional description of the template's purpose.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the template is active and selectable.</summary>
    public bool ActiveFlag { get; set; } = true;
}
