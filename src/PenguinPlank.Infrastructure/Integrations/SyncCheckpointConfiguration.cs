using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="SyncCheckpoint"/>.
/// </summary>
/// <remarks>
/// A unique index on (<see cref="SyncCheckpoint.ConnectionId"/>, <see cref="SyncCheckpoint.Resource"/>)
/// keeps one cursor per connection + resource so incremental polling resumes correctly
/// (requirement 3.9 / R11).
/// </remarks>
internal sealed class SyncCheckpointConfiguration : IEntityTypeConfiguration<SyncCheckpoint>
{
    public void Configure(EntityTypeBuilder<SyncCheckpoint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SyncCheckpoints");

        builder.Property(checkpoint => checkpoint.Resource)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(checkpoint => checkpoint.Cursor)
            .HasMaxLength(400);

        builder.Property(checkpoint => checkpoint.LastReconciledAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(checkpoint => new { checkpoint.ConnectionId, checkpoint.Resource })
            .IsUnique();

        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(checkpoint => checkpoint.ConnectionId)
            .IsRequired();
    }
}
