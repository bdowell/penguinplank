namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned transport shape returned for a single <c>ProductPiece</c> on a list or detail
/// read.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO with no persistence entity or EF type (dependency rule;
/// coding-standards §3) — the API maps the Application <c>ProductPieceView</c> read model into this
/// shape. A piece carries its own actual sizing, finish, and story independently of its variant
/// (requirement 1.6) and reports the <see cref="CareProfileVersionId"/> resolved at production time
/// and preserved thereafter (requirement 2.4). <see cref="ProductionDate"/> is a
/// <see cref="DateTimeOffset"/> so the offset travels with it (requirement A8 §10.3). The opaque
/// <see cref="ETag"/> is echoed as <c>If-Match</c> on a later edit (requirements 6.5, 6.6).
/// </remarks>
public sealed record PieceResponse
{
    /// <summary>The piece's stable, immutable identifier.</summary>
    public required Guid PieceId { get; init; }

    /// <summary>The owning serialized variant's identifier.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The optional business piece code, unique when present.</summary>
    public string? PieceCode { get; init; }

    /// <summary>The piece's actual sizing with its explicit unit, stored independently of the variant.</summary>
    public DimensionsContract? Dimensions { get; init; }

    /// <summary>The piece's actual finish, stored independently of the variant.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional public-safe maker's story, exposed only when public-approved.</summary>
    public string? Story { get; init; }

    /// <summary>The piece's free-form operational status label.</summary>
    public string? Status { get; init; }

    /// <summary>The optional production date, with offset.</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>The care-profile version resolved at production time and preserved thereafter.</summary>
    public Guid? CareProfileVersionId { get; init; }

    /// <summary>The piece's publication lifecycle state as a stable string ("Draft" or "PublicApproved").</summary>
    public required string PublicationState { get; init; }

    /// <summary>Whether the piece is active; an archived piece rejects new transactions.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The opaque concurrency token to echo as <c>If-Match</c> on a subsequent edit.</summary>
    public required string ETag { get; init; }
}
