namespace PenguinPlank.Web.Catalog;

/// <summary>Text normalization shared by the catalog drafts.</summary>
internal static class DraftText
{
    /// <summary>Trims text and returns <see langword="null"/> when nothing remains.</summary>
    /// <param name="value">The entered text.</param>
    /// <returns>The trimmed text, or <see langword="null"/>.</returns>
    internal static string? OrNull(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
