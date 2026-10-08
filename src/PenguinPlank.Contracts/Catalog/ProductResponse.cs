namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned transport shape returned for a single <c>Product</c> on a list or detail read.
/// </summary>
/// <remarks>
/// <para>
/// A standalone Contracts DTO with no persistence entity or EF type (dependency rule;
/// coding-standards §3) — the API maps the Application <c>ProductView</c> read model into this
/// shape, so an EF navigation graph is never serialized. Timestamps are
/// <see cref="DateTimeOffset"/> so the offset travels with the instant (requirement A8 §10.3).
/// Publication state is a stable string rather than a server enum so the contract does not depend
/// on a server type.
/// </para>
/// <para>
/// The opaque <see cref="ETag"/> is the concurrency token the client echoes as <c>If-Match</c> on
/// a later edit (requirements 6.5, 6.6); the API also surfaces it as the HTTP <c>ETag</c> response
/// header. <see cref="InternalNotes"/> is an internal field present only for an authorized caller;
/// the field-level role projection (task 9.4) strips it for callers who may not see it.
/// </para>
/// </remarks>
public sealed record ProductResponse
{
    /// <summary>The product's stable identifier.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>The product family name.</summary>
    public required string Name { get; init; }

    /// <summary>The optional category grouping.</summary>
    public string? Category { get; init; }

    /// <summary>The optional public-safe description.</summary>
    public string? PublicDescription { get; init; }

    /// <summary>The optional internal-only notes; omitted for callers not permitted to see them.</summary>
    public string? InternalNotes { get; init; }

    /// <summary>The publication lifecycle state as a stable string ("Draft" or "PublicApproved").</summary>
    public required string PublicationState { get; init; }

    /// <summary>Whether the product is active; an archived product rejects new transactions.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The opaque concurrency token to echo as <c>If-Match</c> on a subsequent edit.</summary>
    public required string ETag { get; init; }

    /// <summary>The instant the product was created, with offset.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The instant the product was last updated, with offset.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
