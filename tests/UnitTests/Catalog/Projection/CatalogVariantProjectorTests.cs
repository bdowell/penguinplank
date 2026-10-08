using System.Reflection;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog.Projection;

namespace UnitTests.Catalog.Projection;

/// <summary>
/// Behavior of the field-level allowlist projector: Staff receives a role-safe shape with no
/// financial fields present on the object, while Owner receives the financial fields
/// (requirement A2 §5.10, §5.11). Pure, no I/O (coding-standards §1, §7). This proves the
/// server-side strip; the broader use-case coverage lives in task 4.4 and the property test
/// in task 9.5.
/// </summary>
public class CatalogVariantProjectorTests
{
    private static CatalogVariantInternalView BuildInternalView()
    {
        return new CatalogVariantInternalView(
            variantId: Guid.NewGuid(),
            sku: "BOARD-WAL-001",
            productName: "End-grain cutting board",
            retailPrice: 180.00m,
            currency: "USD",
            unitCost: 62.50m,
            wholesaleCost: 95.00m);
    }

    [Fact]
    public void Project_Staff_ReturnsRoleSafeBaseShapeNotOwnerShape()
    {
        var projector = new CatalogVariantProjector();
        CatalogVariantInternalView source = BuildInternalView();

        CatalogVariantView result = projector.Project(source, Role.Staff);

        // The Staff result must be exactly the base shape — not the Owner-derived shape that
        // carries financial fields.
        Assert.IsType<CatalogVariantView>(result, exactMatch: true);
        Assert.IsNotType<OwnerCatalogVariantView>(result);
    }

    [Fact]
    public void Project_Staff_ResultHasNoFinancialMembersAtAll()
    {
        var projector = new CatalogVariantProjector();
        CatalogVariantInternalView source = BuildInternalView();

        CatalogVariantView result = projector.Project(source, Role.Staff);

        // Structural guarantee: the runtime type exposes no cost/margin/profit/wholesale member
        // at all — the financial fields are absent from the object, not present-but-null.
        string[] forbidden = { "UnitCost", "WholesaleCost", "Margin", "Profit", "Cost" };
        PropertyInfo[] properties = result.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            Assert.DoesNotContain(property.Name, forbidden);
        }
    }

    [Fact]
    public void Project_Staff_CarriesTheRoleSafeFields()
    {
        var projector = new CatalogVariantProjector();
        CatalogVariantInternalView source = BuildInternalView();

        CatalogVariantView result = projector.Project(source, Role.Staff);

        Assert.Equal(source.VariantId, result.VariantId);
        Assert.Equal(source.Sku, result.Sku);
        Assert.Equal(source.ProductName, result.ProductName);
        Assert.Equal(source.RetailPrice, result.RetailPrice);
        Assert.Equal(source.Currency, result.Currency);
    }

    [Fact]
    public void Project_Owner_IncludesFinancialFields()
    {
        var projector = new CatalogVariantProjector();
        CatalogVariantInternalView source = BuildInternalView();

        CatalogVariantView result = projector.Project(source, Role.Owner);

        OwnerCatalogVariantView ownerView = Assert.IsType<OwnerCatalogVariantView>(result);
        Assert.Equal(source.UnitCost, ownerView.UnitCost);
        Assert.Equal(source.WholesaleCost, ownerView.WholesaleCost);
        Assert.Equal(source.RetailPrice - source.UnitCost, ownerView.Margin);
    }

    [Fact]
    public void Project_NullSource_Throws()
    {
        var projector = new CatalogVariantProjector();

        Assert.Throws<ArgumentNullException>(() => projector.Project(null!, Role.Owner));
    }

    [Fact]
    public void Project_UndefinedRole_ThrowsRatherThanLeakingFinancials()
    {
        var projector = new CatalogVariantProjector();
        CatalogVariantInternalView source = BuildInternalView();

        // An unrecognized role must fail closed, never fall through to an Owner projection.
        Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(
            () => projector.Project(source, (Role)999));
    }
}
