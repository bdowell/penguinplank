namespace PenguinPlank.Application.Catalog.Projection;

/// <summary>
/// The rich internal representation of a catalog variant, carrying both role-safe fields
/// and Owner-only financial fields.
/// </summary>
/// <remarks>
/// <para>
/// This type is assembled inside the Application layer and is <b>never</b> returned to a
/// caller directly. It is the source a <see cref="IdentityAdministration.Authorization.IRoleProjector{TSource, TResult}"/>
/// reads from to build a role-safe response shape (requirement A2 §5.11). It is a minimal,
/// concrete example that makes the projection mechanism testable now; the full catalog
/// contract DTOs arrive with the catalog endpoints (tasks 9.1 / Contracts).
/// </para>
/// <para>
/// Financial values use <see cref="decimal"/> with an explicit currency, per the money
/// conventions (coding-standards §5). The type is an immutable value object: ordinary data,
/// no I/O, directly testable (coding-standards §1).
/// </para>
/// </remarks>
public sealed record CatalogVariantInternalView
{
    /// <summary>Creates a rich internal variant view.</summary>
    /// <param name="variantId">The variant's stable identifier.</param>
    /// <param name="sku">The variant's unique SKU.</param>
    /// <param name="productName">The display name of the owning product.</param>
    /// <param name="retailPrice">The retail price (visible to every role).</param>
    /// <param name="currency">The ISO 4217 currency code for all monetary values.</param>
    /// <param name="unitCost">The internal unit cost (Owner-only financial field).</param>
    /// <param name="wholesaleCost">The internal wholesale cost (Owner-only financial field).</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="sku"/>, <paramref name="productName"/>, or
    /// <paramref name="currency"/> is null or blank.
    /// </exception>
    public CatalogVariantInternalView(
        System.Guid variantId,
        string sku,
        string productName,
        decimal retailPrice,
        string currency,
        decimal unitCost,
        decimal wholesaleCost)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new System.ArgumentException("A variant requires a SKU.", nameof(sku));
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new System.ArgumentException("A variant requires a product name.", nameof(productName));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new System.ArgumentException("A variant requires a currency.", nameof(currency));
        }

        VariantId = variantId;
        Sku = sku;
        ProductName = productName;
        RetailPrice = retailPrice;
        Currency = currency;
        UnitCost = unitCost;
        WholesaleCost = wholesaleCost;
    }

    /// <summary>The variant's stable identifier (role-safe).</summary>
    public System.Guid VariantId { get; }

    /// <summary>The variant's unique SKU (role-safe).</summary>
    public string Sku { get; }

    /// <summary>The owning product's display name (role-safe).</summary>
    public string ProductName { get; }

    /// <summary>The retail price, visible to every role (role-safe).</summary>
    public decimal RetailPrice { get; }

    /// <summary>The ISO 4217 currency code for all monetary values on this view.</summary>
    public string Currency { get; }

    /// <summary>The internal unit cost. <b>Owner-only</b>; never projected to Staff.</summary>
    public decimal UnitCost { get; }

    /// <summary>The internal wholesale cost. <b>Owner-only</b>; never projected to Staff.</summary>
    public decimal WholesaleCost { get; }

    /// <summary>
    /// The derived gross margin (retail minus unit cost). <b>Owner-only</b>; never projected
    /// to Staff. Exposed as a computed property so the financial figure lives in exactly one
    /// place and is only ever read while building an Owner shape.
    /// </summary>
    public decimal Margin => RetailPrice - UnitCost;
}
