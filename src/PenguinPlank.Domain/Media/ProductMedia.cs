namespace PenguinPlank.Domain.Media;

/// <summary>
/// An explicit foreign-key join associating a <see cref="MediaAsset"/> with a Catalog Product,
/// with the asset's sort order within that product's media.
/// </summary>
/// <remarks>
/// <para>
/// Media associations use an <b>explicit foreign-key join table per owner type</b> — never a
/// polymorphic <c>EntityType</c>/<c>EntityId</c> link (requirement 6.12 / design invariant 9).
/// This type carries a product-typed <see cref="OwnerId"/> and a
/// <see cref="MediaAssetId"/>; variant and piece associations use the sibling
/// <see cref="VariantMedia"/> and <see cref="PieceMedia"/> join tables.
/// </para>
/// <para>
/// It has a <b>composite primary key</b> (<see cref="OwnerId"/>, <see cref="MediaAssetId"/>) and
/// therefore does <em>not</em> derive from <c>Entity</c> (no surrogate GUID key, no concurrency
/// token). The composite key prevents the same asset being linked twice to the same product; its
/// key and both foreign keys are configured in Infrastructure.
/// </para>
/// </remarks>
public class ProductMedia
{
    /// <summary>The owning Catalog Product. The typed foreign key and part of the composite key.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The linked <see cref="MediaAsset"/>. Part of the composite key.</summary>
    public Guid MediaAssetId { get; set; }

    /// <summary>The sort order of the asset within this product's media.</summary>
    public int SortOrder { get; set; }
}
