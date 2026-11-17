namespace IntegrationTests.Performance;

/// <summary>
/// Describes the shape of the representative catalog dataset the performance harness seeds
/// (requirement A9 §11.1, §11.3).
/// </summary>
/// <remarks>
/// <para>
/// Variants alternate between <c>Quantity</c> and <c>Serialized</c> tracking; only the serialized
/// variants own <c>ProductPiece</c> rows, so the piece count is
/// <c>serializedVariants × PiecesPerSerializedVariant</c>. The <see cref="Reference"/> shape is
/// exactly the documented envelope (5,000 variants, 50,000 pieces). Stock lines are a Phase B+
/// workflow and are intentionally not seeded (requirement 11.4).
/// </para>
/// </remarks>
/// <param name="ProductCount">The number of products.</param>
/// <param name="VariantsPerProduct">The number of variants under each product. Must be even.</param>
/// <param name="PiecesPerSerializedVariant">The number of pieces under each serialized variant.</param>
public sealed record CatalogDatasetSpec(int ProductCount, int VariantsPerProduct, int PiecesPerSerializedVariant)
{
    /// <summary>The SKU prefix every seeded variant carries; lets the seeder and tools recognize seeded rows.</summary>
    public const string SkuPrefix = "PERF-";

    /// <summary>The piece-code prefix every seeded piece carries.</summary>
    public const string PieceCodePrefix = "PERF-P-";

    /// <summary>The largest per-variant piece count the three-digit piece-code suffix can keep unique.</summary>
    public const int MaximumPiecesPerSerializedVariant = 999;

    /// <summary>The documented planning envelope: 100 products × 50 variants = 5,000 variants; 2,500 serialized × 20 = 50,000 pieces.</summary>
    public static CatalogDatasetSpec Reference { get; } = new(100, 50, 20);

    /// <summary>A small shape for quick local smoke runs and for verifying the seed SQL against the real schema.</summary>
    /// <remarks>Ten products so each of the ten simulated users still owns a disjoint slice of records.</remarks>
    public static CatalogDatasetSpec Smoke { get; } = new(10, 4, 3);

    /// <summary>Gets the total number of variants the spec produces.</summary>
    public int VariantCount => ProductCount * VariantsPerProduct;

    /// <summary>Gets the number of serialized variants (half of all variants).</summary>
    public int SerializedVariantCount => VariantCount / 2;

    /// <summary>Gets the total number of pieces the spec produces.</summary>
    public int PieceCount => SerializedVariantCount * PiecesPerSerializedVariant;

    /// <summary>Checks that every dimension is positive and the variant count per product is even.</summary>
    /// <exception cref="ArgumentException">Thrown when the spec cannot produce a consistent dataset.</exception>
    public void Validate()
    {
        if (ProductCount < 1 || VariantsPerProduct < 2 || PiecesPerSerializedVariant < 1)
        {
            throw new ArgumentException(
                "A dataset needs at least 1 product, 2 variants per product, and 1 piece per serialized variant.");
        }

        if (PiecesPerSerializedVariant > MaximumPiecesPerSerializedVariant)
        {
            throw new ArgumentException(
                $"PiecesPerSerializedVariant must not exceed {MaximumPiecesPerSerializedVariant} (piece codes use a three-digit suffix).");
        }

        if (VariantsPerProduct % 2 != 0)
        {
            throw new ArgumentException(
                "VariantsPerProduct must be even so serialized and quantity variants alternate evenly.");
        }
    }
}
