using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="ChannelListing"/>.
/// </summary>
/// <remarks>
/// Maps the required channel/external-id/status columns (requirement 1.9), the optional URL,
/// readiness, and last-verified fields, the owning-variant foreign key, and a hot lookup index
/// on (<see cref="ChannelListing.VariantId"/>, <see cref="ChannelListing.Channel"/>) from the
/// design index strategy. A listing is a stored reference only in Phase A (requirement 1.10).
/// </remarks>
internal sealed class ChannelListingConfiguration : IEntityTypeConfiguration<ChannelListing>
{
    public void Configure(EntityTypeBuilder<ChannelListing> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ChannelListings");

        builder.Property(listing => listing.Channel)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(listing => listing.ExternalId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(listing => listing.Url)
            .HasMaxLength(2000);

        builder.Property(listing => listing.Status)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(listing => listing.ReadinessState)
            .HasMaxLength(64);

        builder.Property(listing => listing.LastVerifiedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(listing => listing.LastVerifiedSource)
            .HasMaxLength(200);

        builder.HasIndex(listing => new { listing.VariantId, listing.Channel });

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(listing => listing.VariantId)
            .IsRequired();
    }
}
