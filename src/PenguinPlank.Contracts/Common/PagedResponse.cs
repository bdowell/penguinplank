namespace PenguinPlank.Contracts.Common;

/// <summary>
/// The versioned transport envelope for a paged <c>GET</c> list response: the items for the
/// requested slice plus the paging context a client needs to render pagination
/// (requirement A4 §6.1).
/// </summary>
/// <remarks>
/// <para>
/// This is a standalone Contracts DTO — a plain, serializable shape with no dependency on a
/// persistence entity, an EF type, or any server implementation (dependency rule;
/// coding-standards §3). The API maps the Application layer's <c>Page{T}</c> read model into this
/// envelope so an EF navigation graph is never serialized. The total count is reported so a client
/// can compute the number of pages without fetching them.
/// </para>
/// </remarks>
/// <typeparam name="T">The item DTO carried by the page.</typeparam>
public sealed record PagedResponse<T>
{
    /// <summary>The items on this page, in their stable server-side sort order.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>The total number of matching records across all pages.</summary>
    public required long TotalCount { get; init; }

    /// <summary>The one-based page number this slice represents.</summary>
    public required int PageNumber { get; init; }

    /// <summary>The effective page size that produced this slice (after server-side clamping).</summary>
    public required int PageSize { get; init; }
}
