using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// EF Core mapping for <see cref="MediaAsset"/>.
/// </summary>
/// <remarks>
/// Maps the metadata of a stored media file whose binary content lives outside the database and
/// web root (requirement 6.8 / A4). The <b>unique index on <see cref="MediaAsset.StorageKey"/></b>
/// keeps the randomized storage key distinct (requirement 6.9). <see cref="MediaAsset.Visibility"/>
/// is stored as its enum value with a <c>Private</c> database default so an asset inserted outside
/// a use case cannot start public-approved; being public-approved is metadata only and never
/// grants anonymous download access (requirement 6.11).
/// </remarks>
internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MediaAssets");

        builder.Property(asset => asset.StorageKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(asset => asset.MimeType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(asset => asset.Checksum)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(asset => asset.Caption)
            .HasMaxLength(1000);

        builder.Property(asset => asset.Role)
            .HasMaxLength(64);

        builder.Property(asset => asset.Visibility)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(MediaVisibility.Private);

        builder.HasIndex(asset => asset.StorageKey)
            .IsUnique();
    }
}
