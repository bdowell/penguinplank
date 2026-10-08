using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Publishing;

namespace PenguinPlank.Infrastructure.Publishing;

/// <summary>
/// EF Core mapping for the designed-only <see cref="PublishingResult"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The draft FK is typed and Restrict-deleted.
/// </remarks>
internal sealed class PublishingResultConfiguration : IEntityTypeConfiguration<PublishingResult>
{
    public void Configure(EntityTypeBuilder<PublishingResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PublishingResults");

        builder.Property(result => result.RemoteId)
            .HasMaxLength(200);

        builder.Property(result => result.Status)
            .HasMaxLength(32);

        builder.Property(result => result.RedactedError)
            .HasMaxLength(2000);

        builder.Property(result => result.CompletedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasOne<PublishingDraft>()
            .WithMany()
            .HasForeignKey(result => result.PublishingDraftId)
            .IsRequired();
    }
}
