namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to create a <c>Product</c> family record.
/// </summary>
/// <remarks>
/// An immutable request value carried across the catalog write boundary. Public-ready content
/// (<see cref="Name"/>, <see cref="PublicDescription"/>) is kept separate from
/// <see cref="InternalNotes"/> so neither overwrites the other (requirement 1.11). A new product
/// starts in <c>Draft</c> publication state (requirement 1.12); that default is applied by the
/// use case and the entity configuration, not supplied here. This record carries intent only and
/// performs no I/O (coding-standards §1, §5).
/// </remarks>
public sealed record CreateProductRequest
{
    /// <summary>The product family name. Required; a public-ready field.</summary>
    public required string Name { get; init; }

    /// <summary>The optional product category grouping (for example, "Cutting boards").</summary>
    public string? Category { get; init; }

    /// <summary>The optional public-safe product description. A public-ready field.</summary>
    public string? PublicDescription { get; init; }

    /// <summary>The optional internal-only notes. An internal field never exposed publicly.</summary>
    public string? InternalNotes { get; init; }
}
