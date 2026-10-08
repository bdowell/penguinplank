using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Wholesale;

namespace PenguinPlank.Infrastructure.Wholesale;

/// <summary>
/// EF Core mapping for the designed-only <see cref="FollowUp"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The account/contact/assignee references are designed-only
/// scalar seams (no FK in Phase A). The composite index on
/// (<see cref="FollowUp.AssigneeId"/>, <see cref="FollowUp.Status"/>, <see cref="FollowUp.DueDate"/>)
/// matches the design's follow-up <c>(assignee, status, date)</c> lookup.
/// </remarks>
internal sealed class FollowUpConfiguration : IEntityTypeConfiguration<FollowUp>
{
    public void Configure(EntityTypeBuilder<FollowUp> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("FollowUps");

        builder.Property(followUp => followUp.Status)
            .HasMaxLength(32);

        builder.Property(followUp => followUp.Note)
            .HasMaxLength(2000);

        builder.Property(followUp => followUp.DueDate)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(followUp => new { followUp.AssigneeId, followUp.Status, followUp.DueDate });
    }
}
