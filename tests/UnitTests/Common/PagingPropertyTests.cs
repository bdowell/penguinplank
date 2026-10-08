using CsCheck;
using PenguinPlank.Application.Common;

namespace UnitTests.Common;

/// <summary>
/// Property 16 (requirement A4 Â§6.1): <em>Paging clamp and total count.</em> The paging
/// primitives form a pure, directly testable policy (coding-standards Â§1): a
/// <see cref="PageRequest"/> captures a caller's raw intent and <see cref="PageRequest.Normalize"/>
/// / <see cref="PageRequest.EffectivePageSize"/> apply the Phase A clamping rules, while
/// <see cref="Page"/> materializes a result slice that preserves the supplied total count. These
/// properties assert that the policy holds across the whole input space:
/// <list type="number">
///   <item><b>Size clamp.</b> For any requested size (<see langword="null"/>, negative, zero, or
///   above the maximum) the effective size always lands in
///   <see cref="PageRequest.MinimumPageSize"/>..<see cref="PageRequest.MaximumPageSize"/>:
///   <see langword="null"/> or below the minimum resolves to <see cref="PageRequest.DefaultPageSize"/>;
///   above the maximum clamps down to <see cref="PageRequest.MaximumPageSize"/>; an in-range value
///   is kept unchanged.</item>
///   <item><b>Page-number clamp.</b> A page number below <see cref="PageRequest.MinimumPageNumber"/>
///   resolves to the minimum; a value at or above it is kept.</item>
///   <item><b>Idempotence.</b> <c>Normalize().Normalize()</c> equals <c>Normalize()</c>.</item>
///   <item><b>Total count preserved.</b> <see cref="Page.Create{T}(System.Collections.Generic.IEnumerable{T}, long, PageRequest)"/>
///   carries the supplied non-negative total count unchanged, snapshots the items (never more than
///   the effective page size), and reports <c>Count == Items.Count</c>.</item>
///   <item><b>Total pages.</b> <see cref="Page{T}.TotalPages"/> equals
///   <c>ceil(totalCount / pageSize)</c> and is zero when the total count is zero.</item>
///   <item><b>Skip.</b> <see cref="PageRequest.Skip"/> equals
///   <c>(EffectivePageNumber - 1) * EffectivePageSize</c> and is non-negative.</item>
/// </list>
/// </summary>
/// <remarks>
/// This is a pure-value property test: it exercises the paging primitives with ordinary values and
/// needs no database, no <c>HttpContext</c>, and no application startup (coding-standards Â§1, Â§7).
/// Generators cover page numbers including negative and zero, page sizes including
/// <see langword="null"/> and a wide int range (negative, zero, in-range, and above the maximum),
/// non-negative total counts, and item lists sized up to and beyond the maximum page size. CsCheck
/// (pinned in <c>UnitTests.csproj</c>) runs each property at <see cref="Iterations"/> samples.
/// </remarks>
public class PagingPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Generates a requested page number spanning negative, zero, one, and large values so the
    /// below-minimum and at-or-above-minimum branches are both exercised.
    /// </summary>
    private static readonly Gen<int> s_genPageNumber =
        Gen.Int[-1000, 100_000];

    /// <summary>
    /// Generates a requested page size: <see langword="null"/> (unspecified) mixed with a wide int
    /// range covering negative, zero, in-range (1..200), and above-maximum values.
    /// </summary>
    private static readonly Gen<int?> s_genRequestedPageSize =
        Gen.Frequency(
            (1, Gen.Int[0, 0].Select(_ => (int?)null)),
            (4, Gen.Int[-1000, 1000].Select(size => (int?)size)));

    /// <summary>Generates a non-negative total count, including zero and large values.</summary>
    private static readonly Gen<long> s_genTotalCount =
        Gen.Long[0, 1_000_000];

    /// <summary>The optional sort direction carried unchanged through normalization.</summary>
    private static readonly Gen<SortDirection> s_genSortDirection =
        Gen.Int[0, 1].Select(index => (SortDirection)index);

    /// <summary>Generates a full page request from raw caller intent.</summary>
    private static readonly Gen<PageRequest> s_genPageRequest =
        Gen.Select(s_genPageNumber, s_genRequestedPageSize, s_genSortDirection)
            .Select(tuple => new PageRequest(tuple.Item1, tuple.Item2, sortDirection: tuple.Item3));

    [Fact]
    public void EffectivePageSize_AnyRequestedSize_IsClampedWithinPolicy()
    {
        s_genRequestedPageSize.Sample(
            requestedSize =>
            {
                int clamped = PageRequest.ClampPageSize(requestedSize);
                int effective = new PageRequest(1, requestedSize).EffectivePageSize;

                // The static helper and the instance property agree.
                Assert.Equal(clamped, effective);

                // The clamped size is always a concrete value within policy bounds.
                Assert.InRange(clamped, PageRequest.MinimumPageSize, PageRequest.MaximumPageSize);

                if (requestedSize is not int size || size < PageRequest.MinimumPageSize)
                {
                    // Unspecified or below the minimum resolves to the default.
                    Assert.Equal(PageRequest.DefaultPageSize, clamped);
                }
                else if (size > PageRequest.MaximumPageSize)
                {
                    // Above the maximum clamps down to the maximum.
                    Assert.Equal(PageRequest.MaximumPageSize, clamped);
                }
                else
                {
                    // An in-range value is kept unchanged.
                    Assert.Equal(size, clamped);
                }
            },
            iter: Iterations);
    }

    [Fact]
    public void EffectivePageNumber_AnyPageNumber_ClampsBelowMinimumToOne()
    {
        s_genPageNumber.Sample(
            pageNumber =>
            {
                int effective = new PageRequest(pageNumber).EffectivePageNumber;

                Assert.True(effective >= PageRequest.MinimumPageNumber);

                int expected = pageNumber < PageRequest.MinimumPageNumber
                    ? PageRequest.MinimumPageNumber
                    : pageNumber;
                Assert.Equal(expected, effective);
            },
            iter: Iterations);
    }

    [Fact]
    public void Normalize_AnyRequest_IsIdempotent()
    {
        s_genPageRequest.Sample(
            request =>
            {
                PageRequest once = request.Normalize();
                PageRequest twice = once.Normalize();

                // Record value equality: a second normalization changes nothing.
                Assert.Equal(once, twice);

                // The normalized values already satisfy the policy.
                Assert.True(once.PageNumber >= PageRequest.MinimumPageNumber);
                Assert.NotNull(once.PageSize);
                Assert.InRange(once.PageSize!.Value, PageRequest.MinimumPageSize, PageRequest.MaximumPageSize);

                // Sort intent is preserved through normalization.
                Assert.Equal(request.SortField, once.SortField);
                Assert.Equal(request.SortDirection, once.SortDirection);
            },
            iter: Iterations);
    }

    [Fact]
    public void Skip_AnyRequest_EqualsZeroBasedOffsetAndIsNonNegative()
    {
        s_genPageRequest.Sample(
            request =>
            {
                int expected = (request.EffectivePageNumber - PageRequest.MinimumPageNumber)
                    * request.EffectivePageSize;

                Assert.Equal(expected, request.Skip);
                Assert.True(request.Skip >= 0);
                Assert.Equal(request.EffectivePageSize, request.Take);
            },
            iter: Iterations);
    }

    [Fact]
    public void Create_AnyRequestAndItems_PreservesTotalCountAndBounds()
    {
        Gen.Select(s_genPageRequest, s_genTotalCount)
            .SelectMany(
                pair => Gen.Int.Array[0, PageRequest.MaximumPageSize + 50]
                    .Select(items => (Request: pair.Item1, Total: pair.Item2, Items: items)))
            .Sample(
                sample =>
                {
                    PageRequest normalized = sample.Request.Normalize();

                    // The page can only ever carry up to the effective page size; a reader never
                    // materializes more rows than the clamped size permits.
                    int takeCount = Math.Min(sample.Items.Length, normalized.EffectivePageSize);
                    int[] pageItems = sample.Items[..takeCount];

                    Page<int> page = Page.Create(pageItems, sample.Total, sample.Request);

                    // Total count is carried through unchanged.
                    Assert.Equal(sample.Total, page.TotalCount);

                    // Paging context reflects the clamped request.
                    Assert.Equal(normalized.EffectivePageNumber, page.PageNumber);
                    Assert.Equal(normalized.EffectivePageSize, page.PageSize);

                    // Count tracks the snapshot and never exceeds the effective page size.
                    Assert.Equal(page.Items.Count, page.Count);
                    Assert.True(page.Count <= page.PageSize);
                    Assert.Equal(pageItems, page.Items);
                },
                iter: Iterations);
    }

    [Fact]
    public void TotalPages_AnyRequestAndTotal_EqualsCeilingAndZeroWhenEmpty()
    {
        Gen.Select(s_genPageRequest, s_genTotalCount)
            .Sample(
                pair =>
                {
                    PageRequest request = pair.Item1;
                    long totalCount = pair.Item2;
                    PageRequest normalized = request.Normalize();

                    Page<int> page = Page.Create(Array.Empty<int>(), totalCount, request);

                    long expected = totalCount == 0
                        ? 0
                        : (totalCount + normalized.EffectivePageSize - 1) / normalized.EffectivePageSize;

                    Assert.Equal(expected, page.TotalPages);

                    // An empty total always yields zero pages regardless of the page size.
                    if (totalCount == 0)
                    {
                        Assert.Equal(0, page.TotalPages);
                        Assert.Equal(0, Page.Empty<int>(request).TotalPages);
                    }
                },
                iter: Iterations);
    }
}
