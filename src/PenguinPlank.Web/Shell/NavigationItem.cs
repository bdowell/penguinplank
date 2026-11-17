namespace PenguinPlank.Web.Shell;

/// <summary>One entry of the primary navigation.</summary>
/// <param name="Label">The visible label.</param>
/// <param name="Href">The relative route.</param>
/// <param name="IsPlaceholder">Whether the target is a Phase A placeholder page.</param>
public sealed record NavigationItem(string Label, string Href, bool IsPlaceholder = false);
