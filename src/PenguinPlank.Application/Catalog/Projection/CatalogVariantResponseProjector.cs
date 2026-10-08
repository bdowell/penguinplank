using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace PenguinPlank.Application.Catalog.Projection;

/// <summary>
/// Projects a <see cref="ProductVariantView"/> read model into the role-safe response shape for an
/// actor: an <see cref="OwnerCatalogVariantResponseView"/> for an Owner and a plain
/// <see cref="CatalogVariantResponseView"/> for Staff.
/// </summary>
/// <remarks>
/// <para>
/// This is the field-level allowlist seam the <c>/api/v1/variants</c> endpoints route every
/// response through (requirement A2 §5.10, §5.11). Instead of mapping the read model straight to a
/// transport DTO, the endpoint projects it for the authenticated actor's role first, so a Staff
/// response is produced from the role-safe base shape and an Owner response from the derived shape.
/// The two shapes differ by their declared members, so a Staff response is structurally incapable
/// of carrying an Owner-only cost/margin/profit field — hiding a browser control is never the sole
/// guard. Phase A's read model carries only Staff-safe sale prices, so the Owner branch adds no
/// extra field today; the point is that any future Owner-only figure is read only on the Owner
/// branch and placed only on <see cref="OwnerCatalogVariantResponseView"/>, which keeps the Staff
/// shape safe by construction (Property 11).
/// </para>
/// <para>
/// The projector is pure — it maps inputs to a value with no I/O, clock, configuration, or ambient
/// state — so it is directly unit-testable (coding-standards §1, §7) and safe to register as a DI
/// singleton: it captures no scoped dependency such as a <c>DbContext</c> (requirement A2 §5.14).
/// </para>
/// </remarks>
public sealed class CatalogVariantResponseProjector
    : IRoleProjector<ProductVariantView, CatalogVariantResponseView>
{
    /// <inheritdoc />
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="System.ComponentModel.InvalidEnumArgumentException">
    /// Thrown when <paramref name="role"/> is not a defined <see cref="Role"/> value, so an
    /// unrecognized role never falls through to an Owner (financial) projection.
    /// </exception>
    public CatalogVariantResponseView Project(ProductVariantView source, Role role)
    {
        System.ArgumentNullException.ThrowIfNull(source);

        return role switch
        {
            Role.Owner => new OwnerCatalogVariantResponseView(
                source.VariantId,
                source.ProductId,
                source.Sku,
                source.Barcode,
                source.TrackingMode,
                source.UnitOfMeasure,
                source.Dimensions,
                source.Finish,
                source.RetailPrice,
                source.WholesalePrice,
                source.CasePack,
                source.CareProfileId,
                source.WoodComposition,
                source.IsActive,
                source.ETagToken),

            // Staff and any non-Owner authenticated role receive the role-safe base shape only. No
            // Owner-only financial field is read or carried; the base holds only sale prices and
            // the other Staff-safe fields.
            Role.Staff => new CatalogVariantResponseView(
                source.VariantId,
                source.ProductId,
                source.Sku,
                source.Barcode,
                source.TrackingMode,
                source.UnitOfMeasure,
                source.Dimensions,
                source.Finish,
                source.RetailPrice,
                source.WholesalePrice,
                source.CasePack,
                source.CareProfileId,
                source.WoodComposition,
                source.IsActive,
                source.ETagToken),

            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(role),
                (int)role,
                typeof(Role)),
        };
    }
}
