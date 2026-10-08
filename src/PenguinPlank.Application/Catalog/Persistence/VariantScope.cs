namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The uniqueness scope a candidate SKU or barcode is checked against when creating or editing a
/// <c>ProductVariant</c>.
/// </summary>
/// <remarks>
/// SKU and barcode uniqueness are catalog-wide business rules (requirements R01 §1.1, §1.2 /
/// design invariant 1): a SKU must be unique across every variant, and a supplied barcode must be
/// unique across every variant. This value tells a <see cref="ICatalogVariantStore"/> which
/// candidate codes to check and, when editing, which existing variant to exclude from the check so
/// a variant never collides with itself. It is an immutable input value with no I/O
/// (coding-standards §1).
/// </remarks>
public sealed record VariantUniquenessQuery
{
    /// <summary>The candidate SKU whose uniqueness is being checked. Required and non-empty.</summary>
    public required string CandidateSku { get; init; }

    /// <summary>The candidate barcode, or <see langword="null"/> when none is supplied.</summary>
    public string? CandidateBarcode { get; init; }

    /// <summary>
    /// The variant to exclude from the uniqueness check (its own current codes), or
    /// <see langword="null"/> when creating a new variant.
    /// </summary>
    public Guid? ExcludeVariantId { get; init; }
}
