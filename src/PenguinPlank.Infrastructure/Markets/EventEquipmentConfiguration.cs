using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Markets;

namespace PenguinPlank.Infrastructure.Markets;

/// <summary>
/// EF Core mapping for the designed-only <see cref="EventEquipment"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). <see cref="EventEquipment.EventId"/> is a designed-only
/// scalar seam (no FK in Phase A).
/// </remarks>
internal sealed class EventEquipmentConfiguration : IEntityTypeConfiguration<EventEquipment>
{
    public void Configure(EntityTypeBuilder<EventEquipment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventEquipment");

        builder.Property(equipment => equipment.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(equipment => equipment.Description)
            .HasMaxLength(2000);

        builder.Property(equipment => equipment.ActiveFlag)
            .HasDefaultValue(true);
    }
}
