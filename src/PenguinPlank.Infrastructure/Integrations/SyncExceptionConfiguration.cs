using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="SyncException"/>.
/// </summary>
/// <remarks>
/// An actionable permanent error queued for owner resolution (requirement 3.11 / R11). The stored
/// <see cref="SyncException.RedactedError"/> is a safe-to-display summary — never secrets or
/// unnecessary PII. <see cref="SyncException.Status"/> is stored as a readable string with an
/// <see cref="SyncExceptionStatus.Open"/> default; a lookup index on
/// (<see cref="SyncException.SyncRunId"/>, <see cref="SyncException.Status"/>) supports the queue.
/// </remarks>
internal sealed class SyncExceptionConfiguration : IEntityTypeConfiguration<SyncException>
{
    public void Configure(EntityTypeBuilder<SyncException> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SyncExceptions");

        builder.Property(exception => exception.AffectedResource)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(exception => exception.RedactedError)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(exception => exception.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(SyncExceptionStatus.Open);

        builder.HasIndex(exception => new { exception.SyncRunId, exception.Status });

        builder.HasOne<SyncRun>()
            .WithMany()
            .HasForeignKey(exception => exception.SyncRunId)
            .IsRequired();
    }
}
