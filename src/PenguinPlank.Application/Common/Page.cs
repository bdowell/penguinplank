using System.Collections.ObjectModel;

namespace PenguinPlank.Application.Common;

/// <summary>
/// One materialized page of a GET list: the items for the requested slice together
/// with the paging context needed to describe it — the total number of matching
/// records across all pages, and the page number and size that produced this slice
/// (requirement A4 §6.1).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Page{T}"/> is a pure, framework-agnostic result shape. It holds no
/// <c>IQueryable</c>, no EF entities, and no HTTP types, so it does not leak a
/// persistence or transport concern across the Application boundary (coding-standards
/// §2, §3). Infrastructure fills it from an already-materialized item list and a count;
/// the API layer maps it to a transport DTO.
/// </para>
/// <para>
/// Construct instances through the <see cref="Page"/> factory, which copies the items
/// into an immutable snapshot and validates the paging context. The carried
/// <see cref="Items"/> collection never exceeds <see cref="PageSize"/> for a well-formed
/// page.
/// </para>
/// </remarks>
/// <typeparam name="T">The item type carried by the page (typically a read DTO).</typeparam>
public sealed class Page<T>
{
    internal Page(IReadOnlyList<T> items, long totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>The items on this page, in their stable sort order. An immutable snapshot.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>The total number of matching records across all pages (requirement A4 §6.1).</summary>
    public long TotalCount { get; }

    /// <summary>The one-based page number this slice represents.</summary>
    public int PageNumber { get; }

    /// <summary>The effective page size that produced this slice.</summary>
    public int PageSize { get; }

    /// <summary>The number of items actually on this page (never greater than <see cref="PageSize"/>).</summary>
    public int Count => Items.Count;

    /// <summary>
    /// The total number of pages implied by <see cref="TotalCount"/> and
    /// <see cref="PageSize"/>. Zero when there are no matching records.
    /// </summary>
    public long TotalPages => TotalCount <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;

    /// <summary><see langword="true"/> when a page after this one exists.</summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary><see langword="true"/> when a page before this one exists.</summary>
    public bool HasPreviousPage => PageNumber > PageRequest.MinimumPageNumber;
}

/// <summary>
/// Factory helpers that build <see cref="Page{T}"/> instances. Kept as a separate
/// non-generic type so the generic page shape carries no static members
/// (coding-standards §8; analyzer CA1000) while callers still get type inference on
/// the item type.
/// </summary>
public static class Page
{
    /// <summary>
    /// Creates a page from a materialized item collection, the total matching count, and
    /// the <see cref="PageRequest"/> that produced it. The request is normalized so the
    /// recorded page number and size reflect the Phase A clamping policy
    /// (requirement A4 §6.1).
    /// </summary>
    /// <typeparam name="T">The item type carried by the page.</typeparam>
    /// <param name="items">The items for this page; copied into an immutable snapshot.</param>
    /// <param name="totalCount">The total number of matching records across all pages; must be non-negative.</param>
    /// <param name="request">The originating page request (clamped via <see cref="PageRequest.Normalize"/>).</param>
    /// <returns>An immutable <see cref="Page{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> or <paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="totalCount"/> is negative.</exception>
    public static Page<T> Create<T>(IEnumerable<T> items, long totalCount, PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        PageRequest normalized = request.Normalize();
        return Create(items, totalCount, normalized.EffectivePageNumber, normalized.EffectivePageSize);
    }

    /// <summary>
    /// Creates a page from explicit paging context. Prefer the
    /// <see cref="Create{T}(IEnumerable{T}, long, PageRequest)"/> overload when a
    /// <see cref="PageRequest"/> is available; this overload exists for callers that
    /// already hold concrete, policy-compliant page values.
    /// </summary>
    /// <typeparam name="T">The item type carried by the page.</typeparam>
    /// <param name="items">The items for this page; copied into an immutable snapshot.</param>
    /// <param name="totalCount">The total number of matching records across all pages; must be non-negative.</param>
    /// <param name="pageNumber">The one-based page number; must be at least <see cref="PageRequest.MinimumPageNumber"/>.</param>
    /// <param name="pageSize">The effective page size; must be at least <see cref="PageRequest.MinimumPageSize"/>.</param>
    /// <returns>An immutable <see cref="Page{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="totalCount"/> is negative, <paramref name="pageNumber"/> is below the minimum, or <paramref name="pageSize"/> is below the minimum.</exception>
    public static Page<T> Create<T>(IEnumerable<T> items, long totalCount, int pageNumber, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, PageRequest.MinimumPageNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, PageRequest.MinimumPageSize);

        IReadOnlyList<T> snapshot = new ReadOnlyCollection<T>([.. items]);
        return new Page<T>(snapshot, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Creates an empty page for the given request, carrying no items and a total count
    /// of zero. Useful when a filter matches nothing.
    /// </summary>
    /// <typeparam name="T">The item type carried by the page.</typeparam>
    /// <param name="request">The originating page request.</param>
    /// <returns>An empty <see cref="Page{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    public static Page<T> Empty<T>(PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Create<T>([], totalCount: 0, request);
    }
}
