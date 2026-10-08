using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// A read model describing one <c>ProductPiece</c> for an authenticated catalog list or detail
/// view.
/// </summary>
/// <remarks>
/// <para>
/// A flat, immutable snapshot built by the reader from a materialized query — no persistence type
/// crosses the boundary (coding-standards §2, §3). A piece's actual sizing, finish, and story are
/// carried independently of its variant's values (requirement 1.6). The piece reports the specific
/// <see cref="CareProfileVersionId"/> resolved at its production time, which it keeps resolving
/// even after later care edits (requirement 2.4). Its public <see cref="Story"/> is only exposed
/// once <see cref="PublicationState"/> reaches public-approved (requirements 2.6, 2.7). The opaque
/// <see cref="ETagToken"/> supports a subsequent <c>If-Match</c> edit (requirements 6.5, 6.6).
/// </para>
/// </remarks>
public sealed record ProductPieceView
{
    /// <summary>The piece's stable, immutable identifier (requirement 1.6).</summary>
    public required Guid PieceId { get; init; }

    /// <summary>The owning serialized variant's identifier.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The optional business piece code, unique when present (requirement 1.7).</summary>
    public string? PieceCode { get; init; }

    /// <summary>The piece's actual sizing with its explicit unit, stored independently of the variant.</summary>
    public Dimensions? Dimensions { get; init; }

    /// <summary>The piece's actual finish, stored independently of the variant.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional public-safe maker's story. A public-ready field exposed only when public-approved.</summary>
    public string? Story { get; init; }

    /// <summary>The piece's free-form operational status label.</summary>
    public string? Status { get; init; }

    /// <summary>The optional production date (date-only intent; see A8 time handling).</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>The care-profile version resolved at production time and preserved thereafter (requirement 2.4).</summary>
    public Guid? CareProfileVersionId { get; init; }

    /// <summary>The piece's publication lifecycle state.</summary>
    public required PublicationState PublicationState { get; init; }

    /// <summary>Whether the piece is active; an archived piece rejects new transactions (requirement 1.8).</summary>
    public required bool IsActive { get; init; }

    /// <summary>The opaque concurrency token (encoded ETag) to echo on a subsequent edit.</summary>
    public required string ETagToken { get; init; }
}
