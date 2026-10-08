using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The fully decided set of values a use case hands a <see cref="ICatalogPieceStore"/> to insert a
/// new <c>ProductPiece</c> after the pure policies have accepted it.
/// </summary>
/// <remarks>
/// <para>
/// The use case decides piece-code uniqueness, the archived-master-data guard, the resolved
/// care-profile version in effect at production time (requirement 2.4), and the always-<c>Draft</c>
/// initial publication state (requirements 1.12, 2.6, 2.7) <em>before</em> constructing this record
/// (coding-standards §1). The store writes exactly what the use case decided; it assigns no
/// business value of its own. This is an immutable value with no I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record NewPieceRecord
{
    /// <summary>The identifier the use case generated for the new piece.</summary>
    public required Guid PieceId { get; init; }

    /// <summary>The owning serialized variant.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The initial publication state — always <see cref="PublicationState.Draft"/> (requirements 1.12, 2.6).</summary>
    public required PublicationState PublicationState { get; init; }

    /// <summary>The instant the piece was created, resolved from the injected time provider.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The optional business piece code, when supplied.</summary>
    public string? PieceCode { get; init; }

    /// <summary>The piece's optional actual sizing, when supplied.</summary>
    public Dimensions? Dimensions { get; init; }

    /// <summary>The piece's optional actual finish, when supplied.</summary>
    public string? Finish { get; init; }

    /// <summary>The optional public-safe maker's story for this piece.</summary>
    public string? Story { get; init; }

    /// <summary>The optional free-form operational status label.</summary>
    public string? Status { get; init; }

    /// <summary>The optional production instant recorded for the piece.</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>
    /// The care-profile version in effect at production time that this piece preserves
    /// (requirement 2.4), or <see langword="null"/> when the variant references no care profile.
    /// </summary>
    public Guid? CareProfileVersionId { get; init; }
}
