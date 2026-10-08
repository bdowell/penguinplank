using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog.Projection;

/// <summary>
/// The common, role-safe projected shape of a catalog variant for a list or detail read. Every
/// member here is on the <b>all-authenticated-roles allowlist</b>: it is safe for any
/// authenticated role, including Staff, to see.
/// </summary>
/// <remarks>
/// <para>
/// This is the response-level counterpart of the minimal <see cref="CatalogVariantView"/> demo
/// shape: it carries the full set of Staff-safe variant fields the API returns (identity, SKU,
/// sizing, finish, <b>sale</b> prices, case pack, care reference, wood composition, active flag,
/// concurrency token). Retail and wholesale prices are <em>selling</em> prices — the specification
/// grants Staff access to sale prices (requirements §28 glossary; design "Authorization policy
/// design") — so they live on this base. Owner-only <b>cost</b>, margin, and profit figures do
/// <b>not</b> live here; they belong solely on <see cref="OwnerCatalogVariantResponseView"/>, the
/// derived shape, so a Staff projection — which returns this base type — is structurally incapable
/// of carrying one even in principle (requirement A2 §5.10, §5.11; Property 11).
/// </para>
/// <para>
/// Separate shapes per role are preferred over a single shape with nullable Owner fields: a
/// present-but-null cost member would still appear in the serialized object and in the API
/// contract. Structural absence is the stronger guarantee the field-level allowlist requires. The
/// catalog read models (<see cref="ProductVariantView"/>) carry no cost/margin/profit field today,
/// so the Owner derived shape adds none yet; it is the explicit, single place any such field
/// attaches when a later phase extends the schema, which keeps the Staff shape safe by
/// construction rather than by remembering to strip a field.
/// </para>
/// </remarks>
public record CatalogVariantResponseView
{
    /// <summary>Creates the role-safe base variant response view.</summary>
    /// <param name="variantId">The variant's stable identifier.</param>
    /// <param name="productId">The owning product's identifier.</param>
    /// <param name="sku">The variant's unique SKU.</param>
    /// <param name="barcode">The optional barcode, unique when present.</param>
    /// <param name="trackingMode">The variant's stock tracking mode.</param>
    /// <param name="unitOfMeasure">The explicit unit the variant's quantity is counted or measured in.</param>
    /// <param name="dimensions">The variant's sizing with its explicit unit, when supplied.</param>
    /// <param name="finish">The optional surface finish description.</param>
    /// <param name="retailPrice">The optional retail (sale) price. Role-safe.</param>
    /// <param name="wholesalePrice">The optional wholesale (sale) price. Role-safe.</param>
    /// <param name="casePack">The optional number of units in a wholesale case pack.</param>
    /// <param name="careProfileId">The optional care profile this variant references.</param>
    /// <param name="woodComposition">The variant's wood composition; empty when none is recorded.</param>
    /// <param name="isActive">Whether the variant is active.</param>
    /// <param name="eTagToken">The opaque concurrency token to echo on a subsequent edit.</param>
    public CatalogVariantResponseView(
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
    {
        System.ArgumentNullException.ThrowIfNull(woodComposition);

        VariantId = variantId;
        ProductId = productId;
        Sku = sku;
        Barcode = barcode;
        TrackingMode = trackingMode;
        UnitOfMeasure = unitOfMeasure;
        Dimensions = dimensions;
        Finish = finish;
        RetailPrice = retailPrice;
        WholesalePrice = wholesalePrice;
        CasePack = casePack;
        CareProfileId = careProfileId;
        WoodComposition = woodComposition;
        IsActive = isActive;
        ETagToken = eTagToken;
    }

    /// <summary>The variant's stable identifier.</summary>
    public System.Guid VariantId { get; }

    /// <summary>The owning product's identifier.</summary>
    public System.Guid ProductId { get; }

    /// <summary>The variant's unique SKU.</summary>
    public string Sku { get; }

    /// <summary>The optional barcode, unique when present.</summary>
    public string? Barcode { get; }

    /// <summary>The variant's stock tracking mode.</summary>
    public TrackingMode TrackingMode { get; }

    /// <summary>The explicit unit the variant's quantity is counted or measured in.</summary>
    public string UnitOfMeasure { get; }

    /// <summary>The variant's sizing with its explicit unit, when supplied.</summary>
    public Dimensions? Dimensions { get; }

    /// <summary>The optional surface finish description.</summary>
    public string? Finish { get; }

    /// <summary>The optional retail (sale) price. Role-safe — Staff may see sale prices.</summary>
    public decimal? RetailPrice { get; }

    /// <summary>The optional wholesale (sale) price. Role-safe — Staff may see sale prices.</summary>
    public decimal? WholesalePrice { get; }

    /// <summary>The optional number of units in a wholesale case pack.</summary>
    public int? CasePack { get; }

    /// <summary>The optional care profile this variant references.</summary>
    public System.Guid? CareProfileId { get; }

    /// <summary>The variant's wood composition; empty when none is recorded.</summary>
    public IReadOnlyList<WoodComponent> WoodComposition { get; }

    /// <summary>Whether the variant is active; an archived variant rejects new transactions.</summary>
    public bool IsActive { get; }

    /// <summary>The opaque concurrency token to echo as <c>If-Match</c> on a subsequent edit.</summary>
    public string ETagToken { get; }
}
