using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Publishing;

namespace PenguinPlank.Infrastructure.Publishing;

/// <summary>
/// EF Core mapping for the designed-only <see cref="PublishingDraft"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The source catalog-reference link is a designed-only
/// scalar seam (no FK in Phase A).
/// </remarks>
internal sealed class PublishingDraftConfiguration : IEntityTypeConfiguration<PublishingDraft>
{
    public void Configure(EntityTypeBuilder<PublishingDraft> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PublishingDrafts");

        builder.Property(draft => draft.ReferenceType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(draft => draft.Destination)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(draft => draft.ContentSnapshot)
            .HasMaxLength(8000);

        builder.Property(draft => draft.ApprovedFields)
            .HasMaxLength(4000);

        builder.Property(draft => draft.ApprovedMedia)
            .HasMaxLength(4000);

        builder.Property(draft => draft.ScheduledAtUtc)
            .HasColumnType("datetimeoffset");
    }
}
