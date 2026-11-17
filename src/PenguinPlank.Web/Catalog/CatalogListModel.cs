using PenguinPlank.Contracts.Common;
using PenguinPlank.Web.Api;

namespace PenguinPlank.Web.Catalog;

/// <summary>Fetches one page of a catalog list.</summary>
/// <typeparam name="TItem">The item type.</typeparam>
/// <param name="paging">The page to fetch.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The page, or a failure.</returns>
public delegate Task<ApiResult<PagedResponse<TItem>>> CatalogPageFetcher<TItem>(PagingOptions paging, CancellationToken cancellationToken);

/// <summary>Holds a paged catalog list and the outcome of the last load.</summary>
/// <typeparam name="TItem">The item type.</typeparam>
public sealed class CatalogListModel<TItem>
{
    /// <summary>The page size requested from the server.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Gets the items on the current page.</summary>
    public IReadOnlyList<TItem> Items { get; private set; } = [];

    /// <summary>Gets the total number of matching records.</summary>
    public long TotalCount { get; private set; }

    /// <summary>Gets the one-based current page number.</summary>
    public int PageNumber { get; private set; } = 1;

    /// <summary>Gets the page size the server applied.</summary>
    public int PageSize { get; private set; } = DefaultPageSize;

    /// <summary>Gets the failure from the last load, or <see langword="null"/>.</summary>
    public ApiError? Error { get; private set; }

    /// <summary>Gets a value indicating whether a load is in flight.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets a value indicating whether the first load has finished, successfully or not.</summary>
    public bool HasLoaded { get; private set; }

    /// <summary>Gets a value indicating whether an earlier page exists.</summary>
    public bool HasPrevious => PageNumber > 1;

    /// <summary>Gets a value indicating whether a later page exists.</summary>
    public bool HasNext => (long)PageNumber * PageSize < TotalCount;

    /// <summary>Gets a value indicating whether the load succeeded and found nothing.</summary>
    public bool IsEmpty => HasLoaded && Error is null && Items.Count == 0;

    /// <summary>Gets the text for the last failure, or <see langword="null"/>.</summary>
    public string? ErrorMessage => Error is null ? null : CatalogMessages.DescribeLoad(Error);

    /// <summary>Loads a page. On failure the previous items stay visible and <see cref="Error"/> is set.</summary>
    /// <param name="fetch">Fetches the page.</param>
    /// <param name="pageNumber">The one-based page to load.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the load finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fetch"/> is <see langword="null"/>.</exception>
    public async Task LoadAsync(CatalogPageFetcher<TItem> fetch, int pageNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fetch);

        IsLoading = true;
        try
        {
            ApiResult<PagedResponse<TItem>> result = await fetch(new PagingOptions(Math.Max(1, pageNumber), DefaultPageSize), cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && result.Value is { } page)
            {
                Items = page.Items;
                TotalCount = page.TotalCount;
                PageNumber = page.PageNumber;
                PageSize = page.PageSize;
                Error = null;
            }
            else
            {
                Error = result.Error;
            }
        }
        finally
        {
            IsLoading = false;
            HasLoaded = true;
        }
    }
}
