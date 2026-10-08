using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="SyncRun"/>.
/// </summary>
/// <remarks>
/// A run is sync history (requirement 3.12 / R11); it must never be cascade-deleted (the
/// model-wide restrict default applies to its exceptions). A lookup index on
/// (<see cref="SyncRun.ConnectionId"/>, <see cref="SyncRun.StartedAtUtc"/>) supports listing a
/// connection's recent runs.
/// </remarks>
internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SyncRuns");

        builder.Property(run => run.Resource)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(run => run.StartedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(run => run.CompletedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(run => run.LastSuccessAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(run => new { run.ConnectionId, run.StartedAtUtc });

        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(run => run.ConnectionId)
            .IsRequired();
    }
}
