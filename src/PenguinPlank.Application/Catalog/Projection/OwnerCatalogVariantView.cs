namespace PenguinPlank.Application.Catalog.Projection;

/// <summary>
/// The Owner-only projected shape of a catalog variant: the role-safe base plus the
/// financial fields only an Owner may see.
/// </summary>
/// <remarks>
/// <para>
/// This shape adds unit cost, wholesale cost, and margin to the role-safe
/// <see cref="CatalogVariantView"/> (requirement A2 §5.10, §5.11). It is produced only when
/// the actor is an Owner. Because the financial members exist on this derived type and never
/// on the base, a Staff projection (which returns the base type) has no place to hold them.
/// </para>
/// </remarks>
public sealed record OwnerCatalogVariantView : CatalogVariantView
{
    /// <summary>Creates the Owner view, including the Owner-only financial fields.</summary>
    /// <param name="variantId">The variant's stable identifier.</param>
    /// <param name="sku">The variant's unique SKU.</param>
    /// <param name="productName">The owning product's display name.</param>
    /// <param name="retailPrice">The retail price.</param>
    /// <param name="currency">The ISO 4217 currency code for monetary values.</param>
    /// <param name="unitCost">The internal unit cost (Owner-only).</param>
    /// <param name="wholesaleCost">The internal wholesale cost (Owner-only).</param>
    /// <param name="margin">The derived gross margin (Owner-only).</param>
    public OwnerCatalogVariantView(
        System.Guid variantId,
        string sku,
        string productName,
        decimal retailPrice,
        string currency,
        decimal unitCost,
        decimal wholesaleCost,
        decimal margin)
        : base(variantId, sku, productName, retailPrice, currency)
    {
        UnitCost = unitCost;
        WholesaleCost = wholesaleCost;
        Margin = margin;
    }

    /// <summary>The internal unit cost. Owner-only financial field.</summary>
    public decimal UnitCost { get; }

    /// <summary>The internal wholesale cost. Owner-only financial field.</summary>
    public decimal WholesaleCost { get; }

    /// <summary>The derived gross margin. Owner-only financial field.</summary>
    public decimal Margin { get; }
}
