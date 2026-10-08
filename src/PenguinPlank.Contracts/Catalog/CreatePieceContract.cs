namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned request body for creating a <c>ProductPiece</c> (one physical individual item)
/// under a serialized variant via <c>POST /api/v1/pieces</c>.
/// </summary>
/// <remarks>
/// A standalone Contracts DTO carrying intent only (dependency rule; coding-standards §3). A piece
/// records its own actual sizing, finish, and maker's story independently of its variant
/// (requirement 1.6); its optional <see cref="PieceCode"/> is unique when supplied
/// (requirement 1.7). A new piece's story starts in <c>Draft</c> and is never published
/// automatically (requirements 1.12, 2.6, 2.7). The care-profile version in effect at production
/// time is resolved server-side from <see cref="ProductionDate"/> (requirement 2.4), not supplied
/// here.
/// </remarks>
public sealed record CreatePieceContract
{
    /// <summary>The owning serialized variant. Required.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The optional business piece code. Unique when supplied.</summary>
    public string? PieceCode { get; init; }

    /// <summary>The piece's optional actual sizing with its explicit unit.</summary>
    public DimensionsContract? Dimensions { get; init; }

    /// <summary>The piece's optional actual finish.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional public-safe maker's story for this individual piece.</summary>
    public string? Story { get; init; }

    /// <summary>The optional free-form operational status label.</summary>
    public string? Status { get; init; }

    /// <summary>The optional production date, with offset; the server resolves the care version in effect at this time.</summary>
    public DateTimeOffset? ProductionDate { get; init; }
}
