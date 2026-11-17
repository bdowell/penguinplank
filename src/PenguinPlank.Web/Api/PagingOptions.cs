namespace PenguinPlank.Web.Api;

/// <summary>Optional paging and sorting parameters shared by catalog list reads.</summary>
/// <param name="Page">The one-based page number; the server default applies when omitted.</param>
/// <param name="PageSize">The requested page size; the server clamps it.</param>
/// <param name="Sort">The sort field name.</param>
/// <param name="Descending">Whether to sort descending.</param>
public sealed record PagingOptions(int? Page = null, int? PageSize = null, string? Sort = null, bool Descending = false)
{
    internal IEnumerable<KeyValuePair<string, string?>> ToParameters()
    {
        yield return new("page", QueryStringBuilder.Number(Page));
        yield return new("pageSize", QueryStringBuilder.Number(PageSize));
        yield return new("sort", Sort);
        yield return new("desc", QueryStringBuilder.Flag(Descending));
    }
}
