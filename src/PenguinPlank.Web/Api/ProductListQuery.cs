namespace PenguinPlank.Web.Api;

/// <summary>Filters for <c>GET /api/v1/products</c>.</summary>
public sealed record ProductListQuery
{
    /// <summary>Gets the free-text search.</summary>
    public string? Search { get; init; }

    /// <summary>Gets the category filter.</summary>
    public string? Category { get; init; }

    /// <summary>Gets the publication state filter.</summary>
    public string? PublicationState { get; init; }

    /// <summary>Gets a value indicating whether archived records are included.</summary>
    public bool IncludeArchived { get; init; }

    /// <summary>Gets the paging options.</summary>
    public PagingOptions Paging { get; init; } = new();

    /// <summary>Builds the query string, including the leading <c>?</c> when non-empty.</summary>
    /// <returns>The query string.</returns>
    public string ToQueryString() => QueryStringBuilder.Build(
    [
        new("search", Search),
        new("category", Category),
        new("publicationState", PublicationState),
        new("includeArchived", QueryStringBuilder.Flag(IncludeArchived)),
        .. Paging.ToParameters(),
    ]);
}
