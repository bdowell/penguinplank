using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog.Projection;

/// <summary>
/// The Owner-only projected shape of a catalog variant response: the role-safe
/// <see cref="CatalogVariantResponseView"/> plus the single, explicit home for any Owner-only
/// financial field (unit cost, margin, profit) a variant response may carry.
/// </summary>
/// <remarks>
/// <para>
/// This derived shape exists so the Owner-only financial allowlist for a variant response has one
/// structural place to live, separate from the role-safe base (requirement A2 §5.10, §5.11). A
/// Staff projection returns the base <see cref="CatalogVariantResponseView"/> and therefore cannot
/// hold any member declared here even in principle, which is the structural guarantee Property 11
/// asserts.
/// </para>
/// <para>
/// Phase A's catalog read model (<c>ProductVariantView</c>) carries <b>no</b> cost, margin, or
/// profit field yet — the variant's retail and wholesale <em>sale</em> prices are already role-safe
/// and live on the base. So this Owner shape adds no extra member today; it is deliberately the
/// reserved, documented seam that a later phase extends when it introduces a variant-level cost or
/// margin figure, at which point the field is added here (the Owner branch) only, keeping the Staff
/// shape safe by construction. Being a distinct derived type also lets the API and the Property 11
/// test distinguish an Owner response from a Staff one by its runtime type.
/// </para>
/// </remarks>
public sealed record OwnerCatalogVariantResponseView : CatalogVariantResponseView
{
    /// <summary>Creates the Owner variant response view.</summary>
    /// <param name="variantId">The variant's stable identifier.</param>
    /// <param name="productId">The owning product's identifier.</param>
    /// <param name="sku">The variant's unique SKU.</param>
    /// <param name="barcode">The optional barcode, unique when present.</param>
    /// <param name="trackingMode">The variant's stock tracking mode.</param>
    /// <param name="unitOfMeasure">The explicit unit the variant's quantity is counted or measured in.</param>
    /// <param name="dimensions">The variant's sizing with its explicit unit, when supplied.</param>
    /// <param name="finish">The optional surface finish description.</param>
    /// <param name="retailPrice">The optional retail (sale) price.</param>
    /// <param name="wholesalePrice">The optional wholesale (sale) price.</param>
    /// <param name="casePack">The optional number of units in a wholesale case pack.</param>
    /// <param name="careProfileId">The optional care profile this variant references.</param>
    /// <param name="woodComposition">The variant's wood composition; empty when none is recorded.</param>
    /// <param name="isActive">Whether the variant is active.</param>
    /// <param name="eTagToken">The opaque concurrency token to echo on a subsequent edit.</param>
    public OwnerCatalogVariantResponseView(
        System.Guid variantId,
        System.Guid productId,
        string sku,
        string? barcode,
        TrackingMode trackingMode,
        string unitOfMeasure,
        Dimensions? dimensions,
        string? finish,
        decimal? retailPrice,
        decimal? wholesalePrice,
        int? casePack,
        System.Guid? careProfileId,
        IReadOnlyList<WoodComponent> woodComposition,
        bool isActive,
        string eTagToken)
        : base(
            variantId,
            productId,
            sku,
            barcode,
            trackingMode,
            unitOfMeasure,
            dimensions,
            finish,
            retailPrice,
            wholesalePrice,
            casePack,
            careProfileId,
            woodComposition,
            isActive,
            eTagToken)
    {
    }
}
