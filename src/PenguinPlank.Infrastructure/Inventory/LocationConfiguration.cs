using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Inventory;

namespace PenguinPlank.Infrastructure.Inventory;

/// <summary>
/// EF Core mapping for the designed-only <see cref="Location"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). A unique index on <see cref="Location.LocationCode"/>
/// enforces the business code. <see cref="Location.Type"/> is stored as a string for
/// stability. <see cref="Location.EventId"/> is a designed-only scalar seam (no FK in Phase A).
/// </remarks>
internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Locations");

        builder.Property(location => location.LocationCode)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(location => location.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(location => location.Type)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(location => location.ActiveFlag)
            .HasDefaultValue(true);

        builder.HasIndex(location => location.LocationCode)
            .IsUnique();
    }
}
