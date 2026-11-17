namespace PenguinPlank.Web.Api;

/// <summary>Filters for <c>GET /api/v1/pieces</c>.</summary>
public sealed record PieceListQuery
{
    /// <summary>Gets the owning variant filter.</summary>
    public Guid? VariantId { get; init; }

    /// <summary>Gets the free-text search.</summary>
    public string? Search { get; init; }

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
        new("variantId", QueryStringBuilder.Id(VariantId)),
        new("search", Search),
        new("publicationState", PublicationState),
        new("includeArchived", QueryStringBuilder.Flag(IncludeArchived)),
        .. Paging.ToParameters(),
    ]);
}
