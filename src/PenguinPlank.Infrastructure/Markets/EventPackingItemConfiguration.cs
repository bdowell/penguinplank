using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Markets;

namespace PenguinPlank.Infrastructure.Markets;

/// <summary>
/// EF Core mapping for the designed-only <see cref="EventPackingItem"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The event FK is typed and Restrict-deleted. The
/// referenced-item link is a designed-only scalar seam (no FK in Phase A).
/// </remarks>
internal sealed class EventPackingItemConfiguration : IEntityTypeConfiguration<EventPackingItem>
{
    public void Configure(EntityTypeBuilder<EventPackingItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventPackingItems");

        builder.Property(item => item.ReferenceType)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(item => item.EventId)
            .IsRequired();
    }
}
