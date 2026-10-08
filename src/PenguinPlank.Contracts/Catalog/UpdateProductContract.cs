namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned request body for editing a <c>Product</c> via <c>PATCH /api/v1/products/{id}</c>.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO carrying intent only (dependency rule; coding-standards §3). The
/// concurrency token is <b>not</b> part of this body: the edit presents it through the HTTP
/// <c>If-Match</c> header, which the API passes as the expected version so a stale edit is rejected
/// rather than silently overwriting newer state (requirements 6.5, 6.6). Public-ready and internal
/// content remain separate members (requirement 1.11).
/// </remarks>
public sealed record UpdateProductContract
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
