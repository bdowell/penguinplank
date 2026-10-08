namespace PenguinPlank.Application.Common;

/// <summary>
/// The direction a GET list is sorted in. Sorting is applied on top of a stable,
/// deterministic key so that the overall ordering is reproducible across pages
/// (requirement A4 §6.1; design "GET lists" conventions).
/// </summary>
public enum SortDirection
{
    /// <summary>Ascending order (smallest/earliest first). The default.</summary>
    Ascending = 0,

    /// <summary>Descending order (largest/latest first).</summary>
    Descending = 1,
}
