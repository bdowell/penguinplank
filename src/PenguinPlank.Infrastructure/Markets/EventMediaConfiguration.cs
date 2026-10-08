using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Markets;
using PenguinPlank.Domain.Media;

namespace PenguinPlank.Infrastructure.Markets;

/// <summary>
/// EF Core mapping for the designed-only <see cref="EventMedia"/> explicit join between an
/// <see cref="Event"/> and a <see cref="MediaAsset"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). An <b>explicit foreign-key join table per owner type</b>,
/// never a polymorphic link (design invariant 9). The composite primary key
/// (<see cref="EventMedia.EventId"/>, <see cref="EventMedia.MediaAssetId"/>) and both typed
/// foreign keys — to <see cref="Event"/> and the implemented <see cref="MediaAsset"/> — are
/// configured here with the model-wide Restrict delete behavior.
/// </remarks>
internal sealed class EventMediaConfiguration : IEntityTypeConfiguration<EventMedia>
{
    public void Configure(EntityTypeBuilder<EventMedia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventMedia");

        builder.HasKey(link => new { link.EventId, link.MediaAssetId });

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(link => link.EventId)
            .IsRequired();

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(link => link.MediaAssetId)
            .IsRequired();
    }
}
