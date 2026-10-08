namespace PenguinPlank.Application.Common;

/// <summary>
/// A requested slice of a GET list: which page, how large, and the optional stable
/// sort to apply. A <see cref="PageRequest"/> captures the <em>caller's intent</em>;
/// it must be run through <see cref="Normalize"/> before use so that the effective
/// page number and size respect the Phase A paging policy (requirement A4 §6.1).
/// </summary>
/// <remarks>
/// <para>
/// The clamping policy is a pure, directly testable function (coding-standards §1).
/// <see cref="Normalize"/> performs no I/O and depends only on its inputs, so the
/// paging rules can be exercised with ordinary values without starting the
/// application. Infrastructure then uses the normalized <see cref="PageNumber"/> and
/// <see cref="PageSize"/> to materialize items and a total count into a
/// <see cref="Page{T}"/>.
/// </para>
/// <para>
/// <b>Policy (requirement A4 §6.1).</b> The default page size is
/// <see cref="DefaultPageSize"/> (50) and the maximum is
/// <see cref="MaximumPageSize"/> (200). A requested size above the maximum is clamped
/// down to the maximum; a requested size below <see cref="MinimumPageSize"/> (1) — or
/// unspecified via <see langword="null"/> — resolves to the default; a page number
/// below <see cref="MinimumPageNumber"/> (1) is clamped up to 1. Normalization is
/// idempotent: normalizing an already-normalized request leaves it unchanged.
/// </para>
/// </remarks>
public sealed record PageRequest
{
    /// <summary>The smallest permitted page number. Page numbering is one-based.</summary>
    public const int MinimumPageNumber = 1;

    /// <summary>The smallest permitted page size.</summary>
    public const int MinimumPageSize = 1;

    /// <summary>The page size applied when a request does not specify one (requirement A4 §6.1).</summary>
    public const int DefaultPageSize = 50;

    /// <summary>The largest permitted page size; larger requests are clamped down to it (requirement A4 §6.1).</summary>
    public const int MaximumPageSize = 200;

    /// <summary>
    /// Creates a page request from a caller's raw intent. The values are stored as
    /// supplied; call <see cref="Normalize"/> to apply the clamping policy before use.
    /// </summary>
    /// <param name="pageNumber">The one-based page number requested by the caller.</param>
    /// <param name="pageSize">
    /// The requested page size, or <see langword="null"/> when the caller did not
    /// specify one (which <see cref="Normalize"/> resolves to <see cref="DefaultPageSize"/>).
    /// </param>
    /// <param name="sortField">
    /// The optional field name to sort by. <see langword="null"/> or whitespace means
    /// the reader applies its own stable default ordering.
    /// </param>
    /// <param name="sortDirection">The direction to sort in. Defaults to <see cref="SortDirection.Ascending"/>.</param>
    public PageRequest(
        int pageNumber,
        int? pageSize = null,
        string? sortField = null,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        SortField = sortField;
        SortDirection = sortDirection;
    }

    /// <summary>The one-based page number requested by the caller (not yet clamped).</summary>
    public int PageNumber { get; }

    /// <summary>The requested page size, or <see langword="null"/> when unspecified (not yet clamped).</summary>
    public int? PageSize { get; }

    /// <summary>The optional field name to sort by; <see langword="null"/> or whitespace selects the reader's default.</summary>
    public string? SortField { get; }

    /// <summary>The direction to sort in.</summary>
    public SortDirection SortDirection { get; }

    /// <summary>
    /// A conventional first page using the default page size and the reader's default
    /// stable ordering.
    /// </summary>
    public static PageRequest Default { get; } = new PageRequest(MinimumPageNumber, DefaultPageSize);

    /// <summary>
    /// Applies the Phase A paging policy and returns a request whose
    /// <see cref="EffectivePageNumber"/> and <see cref="EffectivePageSize"/> are safe to
    /// use directly. This is a pure function: it depends only on this request's values
    /// and performs no I/O (coding-standards §1).
    /// </summary>
    /// <returns>
    /// A normalized <see cref="PageRequest"/> whose <see cref="PageNumber"/> is at least
    /// <see cref="MinimumPageNumber"/> and whose <see cref="PageSize"/> is a concrete
    /// value within <see cref="MinimumPageSize"/>..<see cref="MaximumPageSize"/>. The
    /// sort field and direction are preserved unchanged.
    /// </returns>
    public PageRequest Normalize()
    {
        int normalizedPageNumber = PageNumber < MinimumPageNumber ? MinimumPageNumber : PageNumber;
        int normalizedPageSize = ClampPageSize(PageSize);

        if (normalizedPageNumber == PageNumber && PageSize == normalizedPageSize)
        {
            return this;
        }

        return new PageRequest(normalizedPageNumber, normalizedPageSize, SortField, SortDirection);
    }

    /// <summary>
    /// The effective page number after applying the clamping policy: the requested page
    /// number, or <see cref="MinimumPageNumber"/> when the request was below it.
    /// </summary>
    public int EffectivePageNumber => PageNumber < MinimumPageNumber ? MinimumPageNumber : PageNumber;

    /// <summary>
    /// The effective page size after applying the clamping policy. Reflects the default
    /// (unspecified or below the minimum) and maximum (clamped down) rules.
    /// </summary>
    public int EffectivePageSize => ClampPageSize(PageSize);

    /// <summary>
    /// The number of items to skip to reach the start of the effective page. Computed
    /// from the clamped page number and size so it is always non-negative.
    /// </summary>
    public int Skip => (EffectivePageNumber - MinimumPageNumber) * EffectivePageSize;

    /// <summary>
    /// The number of items to take for the effective page. Equal to
    /// <see cref="EffectivePageSize"/>; provided for readability at query sites.
    /// </summary>
    public int Take => EffectivePageSize;

    /// <summary>
    /// Clamps a requested page size to the Phase A policy. A pure helper shared by
    /// <see cref="Normalize"/> and <see cref="EffectivePageSize"/>: unspecified
    /// (<see langword="null"/>) or below <see cref="MinimumPageSize"/> resolves to
    /// <see cref="DefaultPageSize"/>; above <see cref="MaximumPageSize"/> clamps down to
    /// the maximum; otherwise the requested value is kept.
    /// </summary>
    /// <param name="requestedPageSize">The caller's requested size, or <see langword="null"/>.</param>
    /// <returns>A concrete size within <see cref="MinimumPageSize"/>..<see cref="MaximumPageSize"/>.</returns>
    public static int ClampPageSize(int? requestedPageSize)
    {
        if (requestedPageSize is not int size || size < MinimumPageSize)
        {
            return DefaultPageSize;
        }

        return size > MaximumPageSize ? MaximumPageSize : size;
    }
}
