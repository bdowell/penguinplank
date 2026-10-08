using PenguinPlank.Api.Endpoints;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Common;
using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Contracts.Common;
using PenguinPlank.Domain.Catalog;

namespace UnitTests.Catalog;

/// <summary>
/// Behavior of the pure <see cref="CatalogContractMapper"/> that maps between Application catalog
/// read models / request records and the versioned Contracts DTOs (task 9.1).
/// </summary>
/// <remarks>
/// The mapping is the non-trivial part of the thin endpoints — the stable string ↔ enum
/// conversions, the dimensions/wood-composition shape mapping, the explicit currency, and the
/// paged-envelope mapping — so it is exercised directly with ordinary values without starting the
/// application (coding-standards §1, §7). Framework glue (route wiring, DI) is covered by the
/// endpoint/integration tests in tasks 9.5/9.6, not retrofitted here.
/// </remarks>
public class CatalogContractMapperTests
{
    [Fact]
    public void ToResponse_ProductView_MapsFieldsAndSurfacesETagAndOffsets()
    {
        DateTimeOffset created = new(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-8));
        DateTimeOffset updated = created.AddHours(1);
        var view = new ProductView
        {
            ProductId = Guid.NewGuid(),
            Name = "Cutting board",
            Category = "Boards",
            PublicDescription = "A board",
            InternalNotes = "cost basis note",
            PublicationState = PublicationState.PublicApproved,
            IsActive = true,
            ETagToken = "token-123",
            CreatedAtUtc = created,
            UpdatedAtUtc = updated,
        };

        ProductResponse response = CatalogContractMapper.ToResponse(view);

        Assert.Equal(view.ProductId, response.ProductId);
        Assert.Equal("Cutting board", response.Name);
        Assert.Equal("Boards", response.Category);
        Assert.Equal("A board", response.PublicDescription);
        Assert.Equal("cost basis note", response.InternalNotes);
        Assert.Equal("PublicApproved", response.PublicationState);
        Assert.True(response.IsActive);
        Assert.Equal("token-123", response.ETag);
        Assert.Equal(created, response.CreatedAt);
        Assert.Equal(updated, response.UpdatedAt);
        // The offset must travel with the instant (requirement A8 §10.3).
        Assert.Equal(TimeSpan.FromHours(-8), response.CreatedAt.Offset);
    }

    [Fact]
    public void ToResponse_VariantView_MapsTrackingModeStringCurrencyDimensionsAndWood()
    {
        var view = new ProductVariantView
        {
            VariantId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Sku = "SKU-1",
            Barcode = "0123",
            TrackingMode = TrackingMode.Serialized,
            UnitOfMeasure = "each",
            Dimensions = new Dimensions { Length = 10.5m, Width = 5m, Unit = "in" },
            Finish = "oiled",
            RetailPrice = 42.00m,
            WholesalePrice = 21.00m,
            CasePack = 6,
            CareProfileId = Guid.NewGuid(),
            WoodComposition = [new WoodComponent(Guid.NewGuid(), 60m), new WoodComponent(Guid.NewGuid())],
            IsActive = true,
            ETagToken = "v-token",
        };

        VariantResponse response = CatalogContractMapper.ToResponse(view);

        Assert.Equal("SKU-1", response.Sku);
        Assert.Equal("Serialized", response.TrackingMode);
        Assert.Equal("USD", response.Currency);
        Assert.Equal(42.00m, response.RetailPrice);
        Assert.NotNull(response.Dimensions);
        Assert.Equal(10.5m, response.Dimensions!.Length);
        Assert.Equal("in", response.Dimensions.Unit);
        Assert.Equal(2, response.WoodComposition.Count);
        Assert.Equal(60m, response.WoodComposition[0].Proportion);
        Assert.Null(response.WoodComposition[1].Proportion);
        Assert.Equal("v-token", response.ETag);
    }

    [Fact]
    public void ToResponse_PieceView_MapsStoryStatusAndPreservedCareVersion()
    {
        Guid careVersion = Guid.NewGuid();
        DateTimeOffset produced = new(2026, 5, 1, 0, 0, 0, TimeSpan.FromHours(-7));
        var view = new ProductPieceView
        {
            PieceId = Guid.NewGuid(),
            VariantId = Guid.NewGuid(),
            PieceCode = "P-9",
            Dimensions = new Dimensions { Diameter = 12m, Unit = "in" },
            Finish = "wax",
            Story = "made from a storm-fallen oak",
            Status = "finished",
            ProductionDate = produced,
            CareProfileVersionId = careVersion,
            PublicationState = PublicationState.Draft,
            IsActive = true,
            ETagToken = "p-token",
        };

        PieceResponse response = CatalogContractMapper.ToResponse(view);

        Assert.Equal("P-9", response.PieceCode);
        Assert.Equal("made from a storm-fallen oak", response.Story);
        Assert.Equal("finished", response.Status);
        Assert.Equal(careVersion, response.CareProfileVersionId);
        Assert.Equal("Draft", response.PublicationState);
        Assert.Equal(produced, response.ProductionDate);
        Assert.Equal(12m, response.Dimensions!.Diameter);
    }

    [Fact]
    public void ToPagedResponse_CarriesTotalCountAndPagingContext()
    {
        var items = new[]
        {
            BuildProductView("A"),
            BuildProductView("B"),
        };
        Page<ProductView> page = Page.Create(items, totalCount: 57, pageNumber: 2, pageSize: 25);

        PagedResponse<ProductResponse> response =
            CatalogContractMapper.ToPagedResponse(page, CatalogContractMapper.ToResponse);

        Assert.Equal(57, response.TotalCount);
        Assert.Equal(2, response.PageNumber);
        Assert.Equal(25, response.PageSize);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal("A", response.Items[0].Name);
    }

    [Fact]
    public void ToRequest_CreateVariant_ParsesTrackingModeAndMapsWoodAndDimensions()
    {
        Guid productId = Guid.NewGuid();
        Guid speciesId = Guid.NewGuid();
        var contract = new CreateVariantContract
        {
            ProductId = productId,
            Sku = "SKU-7",
            TrackingMode = "quantity",
            UnitOfMeasure = "each",
            Dimensions = new DimensionsContract { Length = 3m, Unit = "in" },
            WoodComposition = [new WoodComponentContract { WoodSpeciesId = speciesId, Proportion = 100m }],
        };

        CreateVariantRequest request = CatalogContractMapper.ToRequest(contract);

        Assert.Equal(productId, request.ProductId);
        Assert.Equal("SKU-7", request.Sku);
        Assert.Equal(TrackingMode.Quantity, request.TrackingMode);
        Assert.Equal(3m, request.Dimensions!.Length);
        Assert.Single(request.WoodComposition);
        Assert.Equal(speciesId, request.WoodComposition[0].WoodSpeciesId);
        Assert.Equal(100m, request.WoodComposition[0].Proportion);
    }

    [Fact]
    public void ToRequest_UpdateVariant_CarriesRouteIdAndExpectedVersion()
    {
        Guid variantId = Guid.NewGuid();
        var contract = new UpdateVariantContract { UnitOfMeasure = "each", Finish = "oiled" };

        UpdateVariantRequest request = CatalogContractMapper.ToRequest(contract, variantId, "if-match-token");

        Assert.Equal(variantId, request.VariantId);
        Assert.Equal("if-match-token", request.ExpectedVersion);
        Assert.Equal("oiled", request.Finish);
    }

    [Theory]
    [InlineData("Serialized", TrackingMode.Serialized)]
    [InlineData("serialized", TrackingMode.Serialized)]
    [InlineData("Quantity", TrackingMode.Quantity)]
    public void ParseTrackingMode_KnownValue_ReturnsMode(string value, TrackingMode expected)
    {
        Assert.Equal(expected, CatalogContractMapper.ParseTrackingMode(value));
    }

    [Theory]
    [InlineData("Lot")]
    [InlineData("")]
    [InlineData("12")]
    public void ParseTrackingMode_UnknownValue_ThrowsFormatException(string value)
    {
        Assert.Throws<CatalogContractFormatException>(() => CatalogContractMapper.ParseTrackingMode(value));
    }

    private static ProductView BuildProductView(string name) => new()
    {
        ProductId = Guid.NewGuid(),
        Name = name,
        PublicationState = PublicationState.Draft,
        IsActive = true,
        ETagToken = "t",
        CreatedAtUtc = DateTimeOffset.UnixEpoch,
        UpdatedAtUtc = DateTimeOffset.UnixEpoch,
    };
}
