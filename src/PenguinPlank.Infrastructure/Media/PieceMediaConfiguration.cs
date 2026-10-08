using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// EF Core mapping for the <see cref="PieceMedia"/> explicit join between a Catalog
/// <see cref="ProductPiece"/> and a <see cref="MediaAsset"/>.
/// </summary>
/// <remarks>
/// An <b>explicit foreign-key join table per owner type</b>, never a polymorphic link
/// (requirement 6.12 / design invariant 9). The composite primary key
/// (<see cref="PieceMedia.OwnerId"/>, <see cref="PieceMedia.MediaAssetId"/>) and both typed
/// foreign keys — to <see cref="ProductPiece"/> and <see cref="MediaAsset"/> — are configured
/// here.
/// </remarks>
internal sealed class PieceMediaConfiguration : IEntityTypeConfiguration<PieceMedia>
{
    public void Configure(EntityTypeBuilder<PieceMedia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PieceMedia");

        builder.HasKey(link => new { link.OwnerId, link.MediaAssetId });

        builder.HasOne<ProductPiece>()
            .WithMany()
            .HasForeignKey(link => link.OwnerId)
            .IsRequired();

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(link => link.MediaAssetId)
            .IsRequired();
    }
}
