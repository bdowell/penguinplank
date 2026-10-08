namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned request body for creating a <c>Product</c> family record via
/// <c>POST /api/v1/products</c>.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO carrying intent only (dependency rule; coding-standards §3).
/// Public-ready content (<see cref="Name"/>, <see cref="PublicDescription"/>) is kept separate
/// from <see cref="InternalNotes"/> so neither overwrites the other (requirement 1.11). A new
/// product starts in <c>Draft</c>; publication is an explicit later action, never supplied here
/// (requirement 1.12).
/// </remarks>
public sealed record CreateProductContract
{
    /// <summary>The product family name. Required.</summary>
    public required string Name { get; init; }

    /// <summary>The optional product category grouping.</summary>
    public string? Category { get; init; }

    /// <summary>The optional public-safe product description.</summary>
    public string? PublicDescription { get; init; }

    /// <summary>The optional internal-only notes.</summary>
    public string? InternalNotes { get; init; }
}
