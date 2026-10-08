using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Publishing;

namespace PenguinPlank.Infrastructure.Publishing;

/// <summary>
/// EF Core mapping for the designed-only <see cref="PublishingApproval"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The draft FK is typed and Restrict-deleted. The approver
/// reference is a designed-only scalar seam (no FK in Phase A).
/// </remarks>
internal sealed class PublishingApprovalConfiguration : IEntityTypeConfiguration<PublishingApproval>
{
    public void Configure(EntityTypeBuilder<PublishingApproval> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PublishingApprovals");

        builder.Property(approval => approval.Note)
            .HasMaxLength(2000);

        builder.Property(approval => approval.ApprovedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<PublishingDraft>()
            .WithMany()
            .HasForeignKey(approval => approval.PublishingDraftId)
            .IsRequired();
    }
}
