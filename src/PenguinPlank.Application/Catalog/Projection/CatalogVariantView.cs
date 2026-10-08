namespace PenguinPlank.Application.Catalog.Projection;

/// <summary>
/// The common, role-safe base shape of a projected catalog variant.
/// </summary>
/// <remarks>
/// <para>
/// Every field declared here is on the <b>public/all-roles allowlist</b>: it is safe for any
/// authenticated role, including Staff, to see. There is deliberately no financial member on
/// this base. The Owner-only financial fields live solely on <see cref="OwnerCatalogVariantView"/>,
/// a derived shape, so a Staff projection — which returns this base type populated as itself
/// — cannot carry a financial value even in principle (requirement A2 §5.11).
/// </para>
/// <para>
/// Separate shapes per role are preferred over a single shape with nullable Owner fields: a
/// null-but-present cost field would still appear in the serialized object and in the API
/// contract. Structural absence is the stronger guarantee that task 9.5 / Property 11 asserts.
/// </para>
/// </remarks>
public record CatalogVariantView
{
    /// <summary>Creates the role-safe base view.</summary>
    /// <param name="variantId">The variant's stable identifier.</param>
    /// <param name="sku">The variant's unique SKU.</param>
    /// <param name="productName">The owning product's display name.</param>
    /// <param name="retailPrice">The retail price (visible to every role).</param>
    /// <param name="currency">The ISO 4217 currency code for monetary values.</param>
    public CatalogVariantView(
        System.Guid variantId,
        string sku,
        string productName,
        decimal retailPrice,
        string currency)
    {
        VariantId = variantId;
        Sku = sku;
        ProductName = productName;
        RetailPrice = retailPrice;
        Currency = currency;
    }

    /// <summary>The variant's stable identifier.</summary>
    public System.Guid VariantId { get; }

    /// <summary>The variant's unique SKU.</summary>
    public string Sku { get; }

    /// <summary>The owning product's display name.</summary>
    public string ProductName { get; }

    /// <summary>The retail price, visible to every role.</summary>
    public decimal RetailPrice { get; }

    /// <summary>The ISO 4217 currency code for monetary values on this view.</summary>
    public string Currency { get; }
}
