using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// A read model describing one <c>Product</c> for an authenticated catalog list or detail view.
/// </summary>
/// <remarks>
/// <para>
/// A read model, not an entity: it is a flat, immutable snapshot built by the reader from an
/// already-materialized query so no <c>IQueryable</c>, <c>DbContext</c>, or EF entity leaks across
/// the Application boundary (coding-standards §2, §3). It carries the opaque
/// <see cref="ETagToken"/> so a subsequent edit can present it as an <c>If-Match</c> value
/// (requirements 6.5, 6.6). Public and internal content are distinct members
/// (<see cref="PublicDescription"/> versus <see cref="InternalNotes"/>) so the API's role/public
/// projection can drop internal content structurally (requirement 1.11).
/// </para>
/// </remarks>
public sealed record ProductView
{
    /// <summary>The product's stable identifier.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The product family name.</summary>
    public required string Name { get; init; }

    /// <summary>The optional category grouping.</summary>
    public string? Category { get; init; }

    /// <summary>The optional public-safe description. A public-ready field.</summary>
    public string? PublicDescription { get; init; }

    /// <summary>The optional internal-only notes. An internal field; the API strips it for non-owner/public callers.</summary>
    public string? InternalNotes { get; init; }

    /// <summary>The publication lifecycle state.</summary>
    public required PublicationState PublicationState { get; init; }

    /// <summary>Whether the product is active; an archived product rejects new transactions (requirement 1.8).</summary>
    public required bool IsActive { get; init; }

    /// <summary>The opaque concurrency token (encoded ETag) to echo on a subsequent edit.</summary>
    public required string ETagToken { get; init; }

    /// <summary>The instant the product was created, with offset.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The instant the product was last updated, with offset.</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
