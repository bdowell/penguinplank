using System.Reflection;
using CsCheck;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Projection;
using PenguinPlank.Domain.Catalog;

namespace UnitTests.Catalog.Projection;

/// <summary>
/// Property 11 (task 9.5, requirements A2 §5.10, §5.11): <em>Staff never receives financial data or
/// owner/credential controls.</em> The role projectors are the concrete server-side field-level
/// allowlist, and both <see cref="CatalogVariantProjector"/> (source
/// <see cref="CatalogVariantInternalView"/>) and <see cref="CatalogVariantResponseProjector"/>
/// (source <see cref="ProductVariantView"/>) are pure functions over ordinary values — no I/O,
/// clock, configuration, or ambient state — so these properties exercise them across the whole
/// input space without a database, <c>HttpContext</c>, or application startup (coding-standards §1,
/// §7). They assert four guarantees over arbitrary financial inputs (unit cost, wholesale cost,
/// margin) and arbitrary ids/strings:
/// <list type="number">
///   <item><b>Staff gets the role-safe base shape, structurally.</b> A <see cref="Role.Staff"/>
///   projection's runtime type is exactly the base shape (<see cref="CatalogVariantView"/> /
///   <see cref="CatalogVariantResponseView"/>) and is <em>not</em> the Owner-derived shape, so it
///   has no member capable of carrying a financial/owner-only field — the data is absent, not
///   present-but-null.</item>
///   <item><b>No financial member is reachable on a Staff result.</b> Reflecting over the Staff
///   result's public instance members finds none named for a cost/margin/profit figure, confirming
///   structural absence rather than a hidden-but-present value.</item>
///   <item><b>Owner carries the exact financial values.</b> A <see cref="Role.Owner"/> projection
///   is the Owner-derived shape and carries the exact generated unit cost, wholesale cost, and
///   margin intact.</item>
///   <item><b>Fails closed.</b> An undefined/out-of-range <see cref="Role"/> throws rather than
///   falling through to an Owner (financial) projection, so an unrecognized role never leaks
///   financial data.</item>
/// </list>
/// </summary>
/// <remarks>
/// Generators produce the full financial input space — decimals that are negative, zero, and large
/// in magnitude — alongside random ids, SKUs, names, and sale prices, so the invariant is sampled
/// well beyond a single hand-built example. CsCheck (pinned in <c>UnitTests.csproj</c>) runs each
/// property at <see cref="Iterations"/> samples, well above the design's ≥100 floor.
///
/// <b>Validates: Requirements 5.10, 5.11</b>
/// </remarks>
public class StaffNeverReceivesFinancialDataPropertyTests
{
    /// <summary>
    /// Iterations per CsCheck property. The design mandates ≥100 iterations for every correctness
    /// property; this sits well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Owner-only financial member names that must never appear on a Staff (base) result shape.
    /// Matched against the runtime type's public property names so the guarantee is structural
    /// absence, not a null value. Sale prices (RetailPrice/WholesalePrice) are deliberately absent
    /// from this list: the specification grants Staff access to <em>sale</em> prices.
    /// </summary>
    private static readonly string[] s_forbiddenFinancialMembers =
    {
        "UnitCost",
        "WholesaleCost",
        "Margin",
        "Profit",
        "Cost",
    };

    /// <summary>
    /// Generates an arbitrary financial decimal spanning negative, zero, and large magnitudes so a
    /// projector cannot pass only for a narrow "well-formed price" slice of the input space.
    /// </summary>
    private static readonly Gen<decimal> s_genFinancial =
        Gen.Long[-100_000_000_000L, 100_000_000_000L].Select(scaled => scaled / 100m);

    /// <summary>A short random non-empty text value for ids/skus/names.</summary>
    private static readonly Gen<string> s_genText =
        Gen.String[Gen.Char['a', 'z'], 1, 16];

    /// <summary>
    /// Generates a rich internal variant view with arbitrary financial values and random role-safe
    /// fields. Margin is derived by the view itself (RetailPrice - UnitCost), matching production.
    /// </summary>
    private static readonly Gen<CatalogVariantInternalView> s_genInternalView =
        Gen.Guid.SelectMany(variantId => s_genText.SelectMany(sku =>
            s_genText.SelectMany(productName => s_genFinancial.SelectMany(retailPrice =>
                s_genText.SelectMany(currency => s_genFinancial.SelectMany(unitCost =>
                    s_genFinancial.Select(wholesaleCost => new CatalogVariantInternalView(
                        variantId,
                        "sku-" + sku,
                        "product-" + productName,
                        retailPrice,
                        "C" + currency,
                        unitCost,
                        wholesaleCost))))))));

    /// <summary>
    /// Generates a full catalog variant read model with random role-safe fields including sale
    /// prices across the full decimal range (negative/zero/large), so the response projector is
    /// exercised over the whole input space.
    /// </summary>
    private static readonly Gen<ProductVariantView> s_genProductVariantView =
        Gen.Guid.SelectMany(variantId => Gen.Guid.SelectMany(productId =>
            s_genText.SelectMany(sku => s_genFinancial.SelectMany(retailPrice =>
                s_genFinancial.SelectMany(wholesalePrice => Gen.Int[0, 1].SelectMany(tracking =>
                    Gen.Bool.Select(isActive => new ProductVariantView
                    {
                        VariantId = variantId,
                        ProductId = productId,
                        Sku = "sku-" + sku,
                        Barcode = null,
                        TrackingMode = (TrackingMode)tracking,
                        UnitOfMeasure = "each",
                        Dimensions = null,
                        Finish = null,
                        RetailPrice = retailPrice,
                        WholesalePrice = wholesalePrice,
                        CasePack = null,
                        CareProfileId = null,
                        WoodComposition = [],
                        IsActive = isActive,
                        ETagToken = "etag-" + sku,
                    })))))));

    [Fact]
    public void VariantProjector_Staff_ReturnsBaseShapeWithNoFinancialMemberForAnyFinancialInput()
    {
        var projector = new CatalogVariantProjector();

        s_genInternalView.Sample(
            source =>
            {
                CatalogVariantView result = projector.Project(source, Role.Staff);

                // Structural guarantee: the runtime type is exactly the role-safe base, never the
                // Owner-derived shape that carries financial fields.
                Assert.IsType<CatalogVariantView>(result, exactMatch: true);
                Assert.IsNotType<OwnerCatalogVariantView>(result);
                AssertNoFinancialMemberReachable(result);
            },
            iter: Iterations);
    }

    [Fact]
    public void VariantProjector_Owner_CarriesExactFinancialValuesForAnyFinancialInput()
    {
        var projector = new CatalogVariantProjector();

        s_genInternalView.Sample(
            source =>
            {
                CatalogVariantView result = projector.Project(source, Role.Owner);

                OwnerCatalogVariantView ownerView = Assert.IsType<OwnerCatalogVariantView>(result);
                Assert.Equal(source.UnitCost, ownerView.UnitCost);
                Assert.Equal(source.WholesaleCost, ownerView.WholesaleCost);
                Assert.Equal(source.Margin, ownerView.Margin);
                Assert.Equal(source.RetailPrice - source.UnitCost, ownerView.Margin);
            },
            iter: Iterations);
    }

    [Fact]
    public void VariantProjector_UndefinedRole_FailsClosedInsteadOfProjectingOwnerFinancials()
    {
        var projector = new CatalogVariantProjector();

        // Any role integer outside the defined set must throw rather than fall through to an Owner
        // (financial) projection — never leak financials for an unrecognized role.
        Gen<int> genUndefinedRole = Gen.Int[-1_000, 1_000]
            .Where(value => value != (int)Role.Owner && value != (int)Role.Staff);

        s_genInternalView.SelectMany(source => genUndefinedRole.Select(role => (source, role)))
            .Sample(
                sample =>
                {
                    Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(
                        () => projector.Project(sample.source, (Role)sample.role));
                },
                iter: Iterations);
    }

    [Fact]
    public void ResponseProjector_Staff_ReturnsBaseShapeWithNoOwnerFinancialMemberForAnyInput()
    {
        var projector = new CatalogVariantResponseProjector();

        s_genProductVariantView.Sample(
            source =>
            {
                CatalogVariantResponseView result = projector.Project(source, Role.Staff);

                // Structural guarantee: exactly the role-safe base shape, never the Owner-derived
                // shape that is the home for any Owner-only cost/margin/profit field.
                Assert.IsType<CatalogVariantResponseView>(result, exactMatch: true);
                Assert.IsNotType<OwnerCatalogVariantResponseView>(result);
                AssertNoFinancialMemberReachable(result);

                // Sale prices are role-safe and still carried for Staff.
                Assert.Equal(source.RetailPrice, result.RetailPrice);
                Assert.Equal(source.WholesalePrice, result.WholesalePrice);
            },
            iter: Iterations);
    }

    [Fact]
    public void ResponseProjector_Owner_ReturnsDerivedOwnerShapeCarryingRoleSafeValuesForAnyInput()
    {
        var projector = new CatalogVariantResponseProjector();

        s_genProductVariantView.Sample(
            source =>
            {
                CatalogVariantResponseView result = projector.Project(source, Role.Owner);

                OwnerCatalogVariantResponseView ownerView =
                    Assert.IsType<OwnerCatalogVariantResponseView>(result);
                Assert.Equal(source.VariantId, ownerView.VariantId);
                Assert.Equal(source.Sku, ownerView.Sku);
                Assert.Equal(source.RetailPrice, ownerView.RetailPrice);
                Assert.Equal(source.WholesalePrice, ownerView.WholesalePrice);
            },
            iter: Iterations);
    }

    [Fact]
    public void ResponseProjector_UndefinedRole_FailsClosedInsteadOfProjectingOwnerShape()
    {
        var projector = new CatalogVariantResponseProjector();

        Gen<int> genUndefinedRole = Gen.Int[-1_000, 1_000]
            .Where(value => value != (int)Role.Owner && value != (int)Role.Staff);

        s_genProductVariantView.SelectMany(source => genUndefinedRole.Select(role => (source, role)))
            .Sample(
                sample =>
                {
                    Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(
                        () => projector.Project(sample.source, (Role)sample.role));
                },
                iter: Iterations);
    }

    [Fact]
    public void BaseShapes_StructurallyDeclareNoOwnerOnlyFinancialMember()
    {
        // Independent of generated samples: the base shapes a Staff projection returns declare no
        // financial member at all, so a Staff result cannot carry one even in principle.
        AssertTypeHasNoFinancialMember(typeof(CatalogVariantView));
        AssertTypeHasNoFinancialMember(typeof(CatalogVariantResponseView));
    }

    /// <summary>
    /// Asserts no public instance member reachable on the given result's runtime type is named for
    /// an Owner-only financial figure, so the Staff result is structurally incapable of carrying one.
    /// </summary>
    private static void AssertNoFinancialMemberReachable(object result)
    {
        PropertyInfo[] properties = result.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            Assert.DoesNotContain(property.Name, s_forbiddenFinancialMembers);
        }
    }

    /// <summary>
    /// Asserts a declared base view type exposes no Owner-only financial member, confirming the
    /// structural guarantee at the type level rather than only on a projected instance.
    /// </summary>
    private static void AssertTypeHasNoFinancialMember(Type viewType)
    {
        HashSet<string> declaredNames = viewType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string forbidden in s_forbiddenFinancialMembers)
        {
            Assert.DoesNotContain(forbidden, declaredNames);
        }
    }
}
