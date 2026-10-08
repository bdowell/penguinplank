namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The existing business codes a catalog uniqueness check decides against: the SKUs and barcodes
/// already held by other variants in scope.
/// </summary>
/// <remarks>
/// <para>
/// A use case loads this snapshot through <see cref="ICatalogVariantStore"/>, then feeds the
/// collections to the pure <c>SkuPolicy.ValidateUnique</c> decision (and an equivalent barcode
/// check) <em>before</em> it persists anything (coding-standards §1). The snapshot carries only the
/// codes the decision needs — not EF entities — so no persistence type crosses the boundary
/// (coding-standards §2, §3). The database's unique index remains the authoritative backstop under
/// concurrency; this snapshot supports the up-front business decision and a clear typed rejection.
/// </para>
/// <para>
/// The code the variant being edited currently owns is excluded by the store (via
/// <see cref="VariantUniquenessQuery.ExcludeVariantId"/>) so a variant never collides with itself.
/// This is an immutable value with no I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record CatalogUniquenessSnapshot
{
    /// <summary>The SKUs already held by other variants in scope. Never <see langword="null"/>; may be empty.</summary>
    public required IReadOnlyCollection<string> ExistingSkus { get; init; }

    /// <summary>The non-blank barcodes already held by other variants in scope. Never <see langword="null"/>; may be empty.</summary>
    public required IReadOnlyCollection<string> ExistingBarcodes { get; init; }
}
