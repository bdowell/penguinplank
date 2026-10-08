using System.Reflection;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Projection;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Domain.Catalog;

namespace UnitTests.Catalog.Projection;

/// <summary>
/// Behavior of the response-level field-level allowlist projector that the
/// <c>/api/v1/variants</c> endpoints route every read through (requirement A2 §5.10, §5.11): an
/// Owner receives the derived Owner shape and a Staff actor receives the role-safe base shape that
/// is structurally incapable of carrying an Owner-only cost/margin/profit member. Pure, no I/O
/// (coding-standards §1, §7). The server-wide property coverage is task 9.5 / Property 11.
/// </summary>
public class CatalogVariantResponseProjectorTests
{
    // Owner-only financial member names that must never appear on a Staff (base) response shape.
    // Matched against the runtime type's public property names so the guarantee is structural
    // absence, not null values. Sale prices (RetailPrice/WholesalePrice) are deliberately NOT here:
    // the specification grants Staff access to sale prices.
    private static readonly string[] s_forbiddenFinancialMembers =
    {
        "UnitCost",
        "WholesaleCost",
        "Margin",
        "Profit",
        "Cost",
    };

    private static ProductVariantView BuildView()
    {
        return new ProductVariantView
        {
            VariantId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Sku = "BOARD-WAL-010",
            Barcode = "0123456789012",
            TrackingMode = TrackingMode.Serialized,
            UnitOfMeasure = "each",
            Dimensions = null,
            Finish = "oiled",
            RetailPrice = 180.00m,
            WholesalePrice = 120.00m,
            CasePack = 6,
            CareProfileId = Guid.NewGuid(),
            WoodComposition = [new WoodComponent(Guid.NewGuid(), 100m)],
            IsActive = true,
            ETagToken = "etag-abc",
        };
    }

    [Fact]
    public void Project_Staff_ReturnsRoleSafeBaseShapeNotOwnerShape()
    {
        var projector = new CatalogVariantResponseProjector();
        ProductVariantView source = BuildView();

        CatalogVariantResponseView result = projector.Project(source, Role.Staff);

        Assert.IsType<CatalogVariantResponseView>(result, exactMatch: true);
        Assert.IsNotType<OwnerCatalogVariantResponseView>(result);
    }

    [Fact]
    public void Project_Staff_ResultHasNoOwnerOnlyFinancialMembersAtAll()
    {
        var projector = new CatalogVariantResponseProjector();
        ProductVariantView source = BuildView();

        CatalogVariantResponseView result = projector.Project(source, Role.Staff);

        // Structural guarantee: the runtime type exposes no cost/margin/profit member at all — an
        // Owner-only financial field is absent from the object, not present-but-null.
        PropertyInfo[] properties = result.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            Assert.DoesNotContain(property.Name, s_forbiddenFinancialMembers);
        }
    }

    [Fact]
    public void Project_Staff_CarriesTheRoleSafeFieldsIncludingSalePrices()
    {
        var projector = new CatalogVariantResponseProjector();
        ProductVariantView source = BuildView();

        CatalogVariantResponseView result = projector.Project(source, Role.Staff);

        Assert.Equal(source.VariantId, result.VariantId);
        Assert.Equal(source.ProductId, result.ProductId);
        Assert.Equal(source.Sku, result.Sku);
        Assert.Equal(source.Barcode, result.Barcode);
        Assert.Equal(source.TrackingMode, result.TrackingMode);
        Assert.Equal(source.UnitOfMeasure, result.UnitOfMeasure);
        Assert.Equal(source.Finish, result.Finish);
        // Sale prices are role-safe: Staff may see them.
        Assert.Equal(source.RetailPrice, result.RetailPrice);
        Assert.Equal(source.WholesalePrice, result.WholesalePrice);
        Assert.Equal(source.CasePack, result.CasePack);
        Assert.Equal(source.CareProfileId, result.CareProfileId);
        Assert.Equal(source.WoodComposition, result.WoodComposition);
        Assert.Equal(source.IsActive, result.IsActive);
        Assert.Equal(source.ETagToken, result.ETagToken);
    }

    [Fact]
    public void Project_Owner_ReturnsDerivedOwnerShapeWithRoleSafeFields()
    {
        var projector = new CatalogVariantResponseProjector();
        ProductVariantView source = BuildView();

        CatalogVariantResponseView result = projector.Project(source, Role.Owner);

        // The Owner result is the derived shape — the structural home for any future Owner-only
        // financial field — while still carrying the role-safe fields.
        OwnerCatalogVariantResponseView ownerView =
            Assert.IsType<OwnerCatalogVariantResponseView>(result);
        Assert.Equal(source.VariantId, ownerView.VariantId);
        Assert.Equal(source.Sku, ownerView.Sku);
        Assert.Equal(source.RetailPrice, ownerView.RetailPrice);
        Assert.Equal(source.WholesalePrice, ownerView.WholesalePrice);
    }

    [Fact]
    public void Project_IsConsumedThroughRoleProjectorSeam()
    {
        // The endpoints depend on the IRoleProjector seam, not the concrete type; assert the
        // projector satisfies it.
        var projector = new CatalogVariantResponseProjector();
        Assert.IsAssignableFrom<IRoleProjector<ProductVariantView, CatalogVariantResponseView>>(projector);
    }

    [Fact]
    public void Project_NullSource_Throws()
    {
        var projector = new CatalogVariantResponseProjector();

        Assert.Throws<ArgumentNullException>(() => projector.Project(null!, Role.Owner));
    }

    [Fact]
    public void Project_UndefinedRole_ThrowsRatherThanLeakingFinancials()
    {
        var projector = new CatalogVariantResponseProjector();
        ProductVariantView source = BuildView();

        // An unrecognized role must fail closed, never fall through to an Owner projection.
        Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(
            () => projector.Project(source, (Role)999));
    }
}
