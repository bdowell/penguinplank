using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The fully decided set of values a use case hands a <see cref="ICatalogVariantStore"/> to insert
/// a new <c>ProductVariant</c> after the pure policies have accepted it.
/// </summary>
/// <remarks>
/// <para>
/// The use case makes every business decision first — SKU/barcode uniqueness, dimension validity,
/// wood-proportion validity, and the always-<c>Draft</c> initial publication state — and only then
/// constructs this record for the store to persist (coding-standards §1: decide, then orchestrate
/// the side effect). The store assigns no business value of its own; it writes exactly what the use
/// case decided, so the business rules live in the use case and the Domain policies, never in the
/// persistence adapter. This is an immutable value with no I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record NewVariantRecord
{
    /// <summary>The identifier the use case generated for the new variant.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The owning product.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The accepted SKU.</summary>
    public required string Sku { get; init; }

    /// <summary>The accepted tracking mode.</summary>
    public required TrackingMode TrackingMode { get; init; }

    /// <summary>The accepted unit of measure.</summary>
    public required string UnitOfMeasure { get; init; }

    /// <summary>The initial publication state — always <see cref="PublicationState.Draft"/> (requirement 1.12).</summary>
    public required PublicationState PublicationState { get; init; }

    /// <summary>The instant the variant was created, resolved from the injected time provider.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The accepted barcode, when supplied.</summary>
    public string? Barcode { get; init; }

    /// <summary>The accepted sizing, when supplied.</summary>
    public Dimensions? Dimensions { get; init; }

    /// <summary>The accepted finish, when supplied.</summary>
    public string? Finish { get; init; }

    /// <summary>The accepted retail price in USD, when supplied.</summary>
    public decimal? RetailPrice { get; init; }

    /// <summary>The accepted wholesale price in USD, when supplied.</summary>
    public decimal? WholesalePrice { get; init; }

    /// <summary>The accepted case pack, when supplied.</summary>
    public int? CasePack { get; init; }

    /// <summary>The referenced care profile, when supplied.</summary>
    public Guid? CareProfileId { get; init; }

    /// <summary>The accepted wood composition; empty when none was supplied.</summary>
    public required IReadOnlyList<WoodComponent> WoodComposition { get; init; }
}
