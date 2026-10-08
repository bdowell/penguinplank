using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace PenguinPlank.Application.Catalog.Projection;

/// <summary>
/// Projects a <see cref="CatalogVariantInternalView"/> into the role-safe shape for an actor:
/// an <see cref="OwnerCatalogVariantView"/> for an Owner and a plain
/// <see cref="CatalogVariantView"/> for Staff.
/// </summary>
/// <remarks>
/// <para>
/// This is the concrete, server-side field-level allowlist for catalog variants
/// (requirement A2 §5.11). The projector reads financial fields from the internal view
/// <b>only</b> on the Owner branch; the Staff branch constructs a base
/// <see cref="CatalogVariantView"/> that has no financial members, so a Staff response is
/// structurally incapable of carrying unit cost, wholesale cost, or margin. Hiding a browser
/// control is therefore never the sole guard: the data is absent from the object the API
/// serializes.
/// </para>
/// <para>
/// The projector is pure — it maps inputs to a value with no I/O, clock, configuration, or
/// ambient state — so it is directly unit-testable (coding-standards §1, §7) and safe to
/// register as a DI singleton: it captures no scoped dependency such as a <c>DbContext</c>
/// (requirement A2 §5.14).
/// </para>
/// </remarks>
public sealed class CatalogVariantProjector
    : IRoleProjector<CatalogVariantInternalView, CatalogVariantView>
{
    /// <inheritdoc />
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="System.ComponentModel.InvalidEnumArgumentException">
    /// Thrown when <paramref name="role"/> is not a defined <see cref="Role"/> value, so an
    /// unrecognized role never falls through to an Owner (financial) projection.
    /// </exception>
    public CatalogVariantView Project(CatalogVariantInternalView source, Role role)
    {
        System.ArgumentNullException.ThrowIfNull(source);

        return role switch
        {
            Role.Owner => new OwnerCatalogVariantView(
                source.VariantId,
                source.Sku,
                source.ProductName,
                source.RetailPrice,
                source.Currency,
                source.UnitCost,
                source.WholesaleCost,
                source.Margin),

            // Staff and any non-Owner authenticated role receive the role-safe base shape
            // only. No financial field is read or carried.
            Role.Staff => new CatalogVariantView(
                source.VariantId,
                source.Sku,
                source.ProductName,
                source.RetailPrice,
                source.Currency),

            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(role),
                (int)role,
                typeof(Role)),
        };
    }
}
