using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// EF Core mapping for the <see cref="VariantMedia"/> explicit join between a Catalog
/// <see cref="ProductVariant"/> and a <see cref="MediaAsset"/>.
/// </summary>
/// <remarks>
/// An <b>explicit foreign-key join table per owner type</b>, never a polymorphic link
/// (requirement 6.12 / design invariant 9). The composite primary key
/// (<see cref="VariantMedia.OwnerId"/>, <see cref="VariantMedia.MediaAssetId"/>) and both typed
/// foreign keys — to <see cref="ProductVariant"/> and <see cref="MediaAsset"/> — are configured
/// here.
/// </remarks>
internal sealed class VariantMediaConfiguration : IEntityTypeConfiguration<VariantMedia>
{
    public void Configure(EntityTypeBuilder<VariantMedia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("VariantMedia");

        builder.HasKey(link => new { link.OwnerId, link.MediaAssetId });

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(link => link.OwnerId)
            .IsRequired();

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(link => link.MediaAssetId)
            .IsRequired();
    }
}
