using PenguinPlank.Api.Endpoints;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Common;
using PenguinPlank.Domain.Catalog;

namespace UnitTests.Catalog;

/// <summary>
/// Behavior of the pure <see cref="CatalogQueryBinder"/> that turns raw <c>GET</c>-list
/// query-string parameters into the typed Application query records and the shared paging intent
/// (requirement A4 §6.1; task 9.1).
/// </summary>
public class CatalogQueryBinderTests
{
    [Fact]
    public void BuildPageRequest_DefaultsToFirstPage_WhenPageOmitted()
    {
        PageRequest request = CatalogQueryBinder.BuildPageRequest(page: null, pageSize: null, sort: null, desc: false);

        Assert.Equal(PageRequest.MinimumPageNumber, request.EffectivePageNumber);
        // An unspecified size resolves to the Phase A default (50) through normalization.
        Assert.Equal(PageRequest.DefaultPageSize, request.EffectivePageSize);
        Assert.Equal(SortDirection.Ascending, request.SortDirection);
    }

    [Fact]
    public void BuildPageRequest_ClampsOversizedPageDownToMaximum()
    {
        PageRequest request = CatalogQueryBinder.BuildPageRequest(page: 3, pageSize: 10_000, sort: "name", desc: true);

        Assert.Equal(3, request.EffectivePageNumber);
        Assert.Equal(PageRequest.MaximumPageSize, request.EffectivePageSize);
        Assert.Equal("name", request.SortField);
        Assert.Equal(SortDirection.Descending, request.SortDirection);
    }

    [Fact]
    public void BuildProductQuery_ParsesPublicationStateAndTrimsSearch()
    {
        PageRequest page = CatalogQueryBinder.BuildPageRequest(1, 20, null, false);

        ProductQuery query = CatalogQueryBinder.BuildProductQuery(
            search: "  board  ",
            category: "   ",
            publicationState: "publicapproved",
            includeArchived: true,
            page);

        Assert.Equal("board", query.SearchTerm);
        Assert.Null(query.Category);
        Assert.Equal(PublicationState.PublicApproved, query.PublicationState);
        Assert.True(query.IncludeArchived);
    }

    [Fact]
    public void BuildVariantQuery_ParsesTrackingModeFilter()
    {
        PageRequest page = CatalogQueryBinder.BuildPageRequest(1, 20, null, false);

        CatalogQuery query = CatalogQueryBinder.BuildVariantQuery(
            productId: null,
            search: null,
            trackingMode: "serialized",
            publicationState: null,
            includeArchived: false,
            page);

        Assert.Equal(TrackingMode.Serialized, query.TrackingMode);
        Assert.Null(query.PublicationState);
    }

    [Fact]
    public void BuildProductQuery_UnknownPublicationState_ThrowsFormatException()
    {
        PageRequest page = CatalogQueryBinder.BuildPageRequest(1, 20, null, false);

        Assert.Throws<CatalogContractFormatException>(() =>
            CatalogQueryBinder.BuildProductQuery(null, null, "retired", false, page));
    }

    [Fact]
    public void BuildVariantQuery_UnknownTrackingMode_ThrowsFormatException()
    {
        PageRequest page = CatalogQueryBinder.BuildPageRequest(1, 20, null, false);

        Assert.Throws<CatalogContractFormatException>(() =>
            CatalogQueryBinder.BuildVariantQuery(null, null, "lot", null, false, page));
    }
}
