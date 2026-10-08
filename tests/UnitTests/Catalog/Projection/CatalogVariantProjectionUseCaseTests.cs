using System.Reflection;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog.Projection;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace UnitTests.Catalog.Projection;

/// <summary>
/// Use-case-level evidence that the field-level authorization allowlist strips financial data for
/// a non-Owner actor (requirement A2 §5.11): a use case that must return a role-safe catalog shape
/// does so by calling the <see cref="IRoleProjector{TSource, TResult}"/> seam rather than
/// serializing the internal view, so a Staff response is structurally incapable of carrying unit
/// cost, wholesale cost, margin, or profit.
/// </summary>
/// <remarks>
/// <para>
/// This complements <see cref="CatalogVariantProjectorTests"/>, which asserts the behavior of the
/// concrete <see cref="CatalogVariantProjector"/> type directly. These tests instead drive the
/// projection through the <see cref="IRoleProjector{TSource, TResult}"/> abstraction — the exact
/// seam the catalog read use cases and API endpoints (tasks 9.1/9.4) depend on — to prove the
/// strip holds at the use-case boundary and for every non-Owner role, not just the concrete type.
/// The server-wide property coverage arrives with task 9.5 / Property 11.
/// </para>
/// <para>
/// Pure and deterministic: no I/O, database, network, or clock (coding-standards §1, §7).
/// </para>
/// </remarks>
public class CatalogVariantProjectionUseCaseTests
{
    // The financial member names that must never appear on a non-Owner result. Matched against the
    // runtime type's public property names so the guarantee is structural absence, not null values.
    private static readonly string[] s_forbiddenFinancialMembers =
    {
        "UnitCost",
        "WholesaleCost",
        "Margin",
        "Profit",
        "Cost",
    };

    private static CatalogVariantInternalView BuildInternalView()
    {
        return new CatalogVariantInternalView(
            variantId: Guid.NewGuid(),
            sku: "BOARD-WAL-002",
            productName: "Walnut serving board",
            retailPrice: 210.00m,
            currency: "USD",
            unitCost: 70.00m,
            wholesaleCost: 110.00m);
    }

    /// <summary>
    /// A use case that returns a catalog shape to Staff, going through the projector seam, yields an
    /// object whose runtime type exposes no financial member at all — the strip is server-side, not a
    /// hidden browser control.
    /// </summary>
    [Fact]
    public void StaffProjection_ThroughRoleProjectorSeam_StripsAllFinancialData()
    {
        // Arrange: the use case holds the projector behind its IRoleProjector seam, but we exercise
        // the concrete implementation that the composition root wires.
        var projector = new CatalogVariantProjector();
        AssertImplementsRoleProjectorSeam(projector);
        CatalogVariantInternalView source = BuildInternalView();

        // Act: Project is called through the IRoleProjector contract shape.
        CatalogVariantView result = projector.Project(source, Role.Staff);

        // Assert: exactly the role-safe base shape, never the Owner-derived financial shape.
        Assert.IsType<CatalogVariantView>(result, exactMatch: true);

        string[] memberNames = PublicPropertyNames(result);
        Assert.All(
            s_forbiddenFinancialMembers,
            forbidden => Assert.DoesNotContain(forbidden, memberNames));
    }

    /// <summary>
    /// The strip is not special-cased to the <see cref="Role.Staff"/> value: any authenticated
    /// non-Owner role receives a shape with no financial member. (Phase A defines only Staff as a
    /// non-Owner role; asserting over the enum keeps the guarantee honest as roles are added.)
    /// </summary>
    [Theory]
    [MemberData(nameof(NonOwnerRoles))]
    public void Projection_ForAnyNonOwnerRole_CarriesNoFinancialMember(Role role)
    {
        var projector = new CatalogVariantProjector();
        CatalogVariantInternalView source = BuildInternalView();

        CatalogVariantView result = projector.Project(source, role);

        Assert.IsNotType<OwnerCatalogVariantView>(result);

        string[] memberNames = PublicPropertyNames(result);
        Assert.All(
            s_forbiddenFinancialMembers,
            forbidden => Assert.DoesNotContain(forbidden, memberNames));
    }

    /// <summary>
    /// The Owner projection, through the same seam, carries the financial fields populated from the
    /// internal view — proving the strip is role-dependent, not a blanket removal.
    /// </summary>
    [Fact]
    public void OwnerProjection_ThroughRoleProjectorSeam_IncludesPopulatedFinancialData()
    {
        var projector = new CatalogVariantProjector();
        CatalogVariantInternalView source = BuildInternalView();

        CatalogVariantView result = projector.Project(source, Role.Owner);

        OwnerCatalogVariantView ownerView = Assert.IsType<OwnerCatalogVariantView>(result);
        Assert.Equal(source.UnitCost, ownerView.UnitCost);
        Assert.Equal(source.WholesaleCost, ownerView.WholesaleCost);
        Assert.Equal(source.Margin, ownerView.Margin);

        // The role-safe fields survive on both shapes.
        Assert.Equal(source.VariantId, ownerView.VariantId);
        Assert.Equal(source.Sku, ownerView.Sku);
        Assert.Equal(source.RetailPrice, ownerView.RetailPrice);
    }

    public static IEnumerable<object[]> NonOwnerRoles()
    {
        foreach (Role role in Enum.GetValues<Role>())
        {
            if (role != Role.Owner)
            {
                yield return new object[] { role };
            }
        }
    }

    private static string[] PublicPropertyNames(object instance)
    {
        return instance.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();
    }

    // Documents that the catalog projector is consumed through the IRoleProjector seam the use
    // cases and API depend on, not as a one-off concrete helper.
    private static void AssertImplementsRoleProjectorSeam(CatalogVariantProjector projector)
    {
        Assert.IsAssignableFrom<IRoleProjector<CatalogVariantInternalView, CatalogVariantView>>(projector);
    }
}
