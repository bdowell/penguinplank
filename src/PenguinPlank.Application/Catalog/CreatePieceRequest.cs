namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to create a <c>ProductPiece</c> (one physical individual item) under a
/// serialized variant.
/// </summary>
/// <remarks>
/// <para>
/// An immutable request value carried across the catalog write boundary. A piece records its own
/// actual sizing, finish, and maker's story independently of its variant's values
/// (requirement 1.6), and its optional <c>PieceCode</c> is unique when supplied
/// (requirement 1.7). A new piece's public story starts in <c>Draft</c> and is never published
/// automatically (requirements 1.12, 2.6, 2.7). The <c>CareProfileVersion</c> in effect at
/// production time is resolved and recorded by the use case (requirement 2.4), not supplied here.
/// This record carries intent only and performs no I/O (coding-standards §1, §5).
/// </para>
/// </remarks>
public sealed record CreatePieceRequest
{
    /// <summary>The owning serialized variant. Required and non-empty.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The optional business piece code. Unique across all pieces when supplied (requirement 1.7).</summary>
    public string? PieceCode { get; init; }

    /// <summary>The piece's optional actual sizing with its explicit unit, stored independently of the variant.</summary>
    public Dimensions? Dimensions { get; init; }

    /// <summary>The piece's optional actual finish, stored independently of the variant.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional public-safe maker's story for this individual piece. A public-ready field.</summary>
    public string? Story { get; init; }

    /// <summary>The optional free-form operational status label for the piece.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// The optional production date (date-only intent; see A8 time handling). The use case
    /// resolves the care-profile version in effect at this time (requirement 2.4).
    /// </summary>
    public DateTimeOffset? ProductionDate { get; init; }
}
