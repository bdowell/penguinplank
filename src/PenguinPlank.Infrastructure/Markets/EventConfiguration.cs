using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Markets;

namespace PenguinPlank.Infrastructure.Markets;

/// <summary>
/// EF Core mapping for the designed-only <see cref="Event"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). Public and internal descriptions are separate columns so
/// a public projection cannot leak internal content (design invariant 11). The publication
/// state defaults to <see cref="PublicationState.Draft"/> at the database level. A start-date
/// index supports the design's event <c>(date)</c> lookup.
/// </remarks>
internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Events");

        builder.Property(marketEvent => marketEvent.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(marketEvent => marketEvent.InternalDescription)
            .HasMaxLength(4000);

        builder.Property(marketEvent => marketEvent.PublicDescription)
            .HasMaxLength(4000);

        builder.Property(marketEvent => marketEvent.Venue)
            .HasMaxLength(400);

        builder.Property(marketEvent => marketEvent.Timezone)
            .HasMaxLength(64);

        builder.Property(marketEvent => marketEvent.Status)
            .HasMaxLength(64);

        builder.Property(marketEvent => marketEvent.SetupNotes)
            .HasMaxLength(4000);

        builder.Property(marketEvent => marketEvent.LoadOutNotes)
            .HasMaxLength(4000);

        builder.Property(marketEvent => marketEvent.StartDate)
            .HasColumnType("datetimeoffset");

        builder.Property(marketEvent => marketEvent.EndDate)
            .HasColumnType("datetimeoffset");

        builder.Property(marketEvent => marketEvent.PublicationState)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(PublicationState.Draft);

        builder.HasIndex(marketEvent => marketEvent.StartDate);
    }
}
