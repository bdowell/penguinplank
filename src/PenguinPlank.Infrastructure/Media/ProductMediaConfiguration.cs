using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// EF Core mapping for the <see cref="ProductMedia"/> explicit join between a Catalog
/// <see cref="Product"/> and a <see cref="MediaAsset"/>.
/// </summary>
/// <remarks>
/// This is an <b>explicit foreign-key join table per owner type</b>, never a polymorphic link
/// (requirement 6.12 / design invariant 9). It does not derive from <c>Entity</c>, so the
/// composite primary key (<see cref="ProductMedia.OwnerId"/>, <see cref="ProductMedia.MediaAssetId"/>)
/// and both typed foreign keys — to <see cref="Product"/> and <see cref="MediaAsset"/> — are
/// configured here. The composite key prevents linking the same asset to the same product twice.
/// </remarks>
internal sealed class ProductMediaConfiguration : IEntityTypeConfiguration<ProductMedia>
{
    public void Configure(EntityTypeBuilder<ProductMedia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductMedia");

        builder.HasKey(link => new { link.OwnerId, link.MediaAssetId });

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(link => link.OwnerId)
            .IsRequired();

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(link => link.MediaAssetId)
            .IsRequired();
    }
}
