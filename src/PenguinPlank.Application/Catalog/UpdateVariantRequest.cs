namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to update an existing <c>ProductVariant</c>, including the concurrency
/// token the edit must match.
/// </summary>
/// <remarks>
/// <para>
/// A variant is a mutable aggregate protected by optimistic concurrency: an edit presents the
/// <see cref="ExpectedVersion"/> opaque token and is rejected if stale rather than silently
/// overwriting newer state (requirements 6.5, 6.6). The <c>Sku</c> and <c>TrackingMode</c> are
/// deliberately absent: the SKU identity and the tracking-mode change rule are governed by their
/// own guarded operations (requirement 1.13 / the tracking-mode change policy), not by a general
/// field update. This is an immutable request value with no I/O (coding-standards §1, §5).
/// </para>
/// </remarks>
public sealed record UpdateVariantRequest
{
    /// <summary>The identifier of the variant to update. Required and non-empty.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>
    /// The opaque concurrency token the caller read with the variant (the encoded ETag/If-Match
    /// value). The update applies only when it still matches the stored row (requirements 6.5,
    /// 6.6).
    /// </summary>
    public required string ExpectedVersion { get; init; }

    /// <summary>The optional barcode. Unique when supplied (design invariant 1).</summary>
    public string? Barcode { get; init; }

    /// <summary>The explicit unit the variant's quantity is counted or measured in. Required (requirement 1.1).</summary>
    public required string UnitOfMeasure { get; init; }

    /// <summary>The optional physical sizing with its explicit unit (requirements 1.3, 1.4).</summary>
    public Dimensions? Dimensions { get; init; }

    /// <summary>The optional surface finish description.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional retail price in USD.</summary>
    public decimal? RetailPrice { get; init; }

    /// <summary>The optional wholesale price in USD.</summary>
    public decimal? WholesalePrice { get; init; }

    /// <summary>The optional number of units in a wholesale case pack.</summary>
    public int? CasePack { get; init; }

    /// <summary>The optional care profile this variant references for care guidance.</summary>
    public Guid? CareProfileId { get; init; }
}
