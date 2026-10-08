namespace PenguinPlank.Domain.Media;

/// <summary>
/// An explicit foreign-key join associating a <see cref="MediaAsset"/> with a Catalog
/// ProductPiece, with the asset's sort order within that piece's media.
/// </summary>
/// <remarks>
/// <para>
/// Media associations use an <b>explicit foreign-key join table per owner type</b> — never a
/// polymorphic <c>EntityType</c>/<c>EntityId</c> link (requirement 6.12 / design invariant 9).
/// This type carries a piece-typed <see cref="OwnerId"/> and a <see cref="MediaAssetId"/>.
/// </para>
/// <para>
/// It has a <b>composite primary key</b> (<see cref="OwnerId"/>, <see cref="MediaAssetId"/>) and
/// does <em>not</em> derive from <c>Entity</c>; its key and both foreign keys are configured in
/// Infrastructure.
/// </para>
/// </remarks>
public class PieceMedia
{
    /// <summary>The owning Catalog ProductPiece. The typed foreign key and part of the composite key.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The linked <see cref="MediaAsset"/>. Part of the composite key.</summary>
    public Guid MediaAssetId { get; set; }

    /// <summary>The sort order of the asset within this piece's media.</summary>
    public int SortOrder { get; set; }
}
