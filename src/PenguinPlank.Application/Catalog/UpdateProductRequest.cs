namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to update an existing <c>Product</c>, including the concurrency token the
/// edit must match.
/// </summary>
/// <remarks>
/// <para>
/// A product is a mutable aggregate protected by optimistic concurrency: an edit must present the
/// <see cref="ExpectedVersion"/> (the opaque <c>rowversion</c> token read with the record and
/// surfaced as an HTTP ETag). A stale token is rejected rather than silently overwriting newer
/// state (requirements 6.5, 6.6). Public-ready and internal content remain in separate members so
/// neither overwrites the other (requirement 1.11). This is an immutable request value with no
/// I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record UpdateProductRequest
{
    /// <summary>The identifier of the product to update. Required and non-empty.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>
    /// The opaque concurrency token the caller read with the product (the encoded ETag/If-Match
    /// value). The update applies only when it still matches the stored row (requirements 6.5,
    /// 6.6). Kept as the transport-neutral encoded token so no persistence type leaks across the
    /// boundary.
    /// </summary>
    public required string ExpectedVersion { get; init; }

    /// <summary>The product family name. Required; a public-ready field.</summary>
    public required string Name { get; init; }

    /// <summary>The optional product category grouping.</summary>
    public string? Category { get; init; }

    /// <summary>The optional public-safe product description. A public-ready field.</summary>
    public string? PublicDescription { get; init; }

    /// <summary>The optional internal-only notes. An internal field never exposed publicly.</summary>
    public string? InternalNotes { get; init; }
}
