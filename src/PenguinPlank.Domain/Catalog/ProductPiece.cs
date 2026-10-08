using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// One physical individual item belonging to a serialized <see cref="ProductVariant"/>,
/// identified by a stable immutable <see cref="Entity.Id"/> and an optional unique business
/// <see cref="PieceCode"/>.
/// </summary>
/// <remarks>
/// <para>
/// A piece is a <b>mutable aggregate</b> (its finish, story, status, and publication state
/// change over its life) and derives from <see cref="VersionedEntity"/>. Its identifier is
/// stable and immutable (requirement 1.6); its <see cref="PieceCode"/> is unique when supplied
/// (requirement 1.7) via a filtered unique index.
/// </para>
/// <para>
/// A piece's actual dimensions, finish, and story are stored <b>independently of its
/// variant's values</b> (requirement 1.6) — the per-piece columns here are distinct from the
/// variant columns. The piece records the <see cref="CareProfileVersionId"/> in effect at the
/// time it was produced and continues to resolve that same version after later care edits
/// (requirement 2.4 / design invariant 10). Its public story stays in
/// <see cref="PublicationState.Draft"/> until an explicit publish action (requirements 2.6,
/// 2.7).
/// </para>
/// <para>
/// <b>Designed-only production link.</b> <see cref="ProductionBatchLineId"/> is the
/// designed-only link to the future Production module. No Production entity exists in Phase A,
/// so this is a <b>plain nullable GUID column with no navigation property and no foreign-key
/// constraint</b> to a not-yet-existing table; the FK relationship is established additively
/// when Production is implemented. Modeling it now as a nullable column reserves the seam
/// without forcing a disruptive schema change later (design "additive extension seams").
/// </para>
/// </remarks>
public class ProductPiece : VersionedEntity
{
    /// <summary>The owning serialized <see cref="ProductVariant"/>. Required.</summary>
    public Guid VariantId { get; set; }

    /// <summary>
    /// An optional business piece code. <b>Unique across all pieces when supplied</b>
    /// (requirement 1.7); enforced by a filtered unique index that ignores nulls.
    /// </summary>
    public string? PieceCode { get; set; }

    /// <summary>The piece's actual length. Stored independently of the variant's dimensions.</summary>
    public decimal? Length { get; set; }

    /// <summary>The piece's actual width. Stored independently of the variant's dimensions.</summary>
    public decimal? Width { get; set; }

    /// <summary>The piece's actual thickness/height. Stored independently of the variant's dimensions.</summary>
    public decimal? Thickness { get; set; }

    /// <summary>The piece's actual diameter for a round product. Stored independently of the variant.</summary>
    public decimal? Diameter { get; set; }

    /// <summary>The explicit unit for this piece's dimension values. Required when any dimension is supplied.</summary>
    public string? DimensionUnit { get; set; }

    /// <summary>The piece's actual finish, stored independently of the variant's finish.</summary>
    public string? Finish { get; set; }

    /// <summary>The public-safe maker's story for this individual piece. A public-ready field.</summary>
    public string? Story { get; set; }

    /// <summary>The piece's lifecycle status (a free-form operational status label in Phase A).</summary>
    public string? Status { get; set; }

    /// <summary>The date the piece was produced (date-only intent; see A8 time handling).</summary>
    public DateTimeOffset? ProductionDate { get; set; }

    /// <summary>
    /// The <see cref="CareProfileVersion"/> in effect when the piece was produced. The piece
    /// keeps resolving this exact immutable version even after the care profile is later edited
    /// (requirement 2.4 / design invariant 10).
    /// </summary>
    public Guid? CareProfileVersionId { get; set; }

    /// <summary>
    /// The designed-only link to a future Production batch line. A plain nullable GUID with
    /// <b>no navigation and no FK constraint</b> in Phase A because the Production table does
    /// not exist yet; wired up additively when Production is implemented.
    /// </summary>
    public Guid? ProductionBatchLineId { get; set; }

    /// <summary>
    /// The publication lifecycle state of the piece's public story. Defaults to
    /// <see cref="PublicationState.Draft"/> (requirements 1.12, 2.6, 2.7).
    /// </summary>
    public PublicationState PublicationState { get; set; } = PublicationState.Draft;

    /// <summary>Whether the piece is active. An archived piece remains readable but rejects new transactions.</summary>
    public bool ActiveFlag { get; set; } = true;
}
