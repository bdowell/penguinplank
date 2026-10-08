using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="IntegrationOutbox"/>.
/// </summary>
/// <remarks>
/// The <b>unique index on <see cref="IntegrationOutbox.OperationKey"/></b> ensures an outgoing
/// operation is enqueued at most once (requirement 3.14 / R11; design index strategy). The
/// covering lookup index on
/// (<see cref="IntegrationOutbox.ConnectionId"/>, <see cref="IntegrationOutbox.Status"/>,
/// <see cref="IntegrationOutbox.LeaseExpiresAtUtc"/>) supports lease-based worker dispatch.
/// <see cref="IntegrationOutbox.Status"/> is stored as a readable string with a
/// <see cref="OutboxStatus.Pending"/> default.
/// <para>
/// <b>GUID-key fragmentation strategy (design index strategy).</b> The outbox is a hot,
/// insert-heavy table. The primary key is made <em>non-clustered</em> and the clustered key is
/// placed on the insert-ordered <c>CreatedAtUtc</c> column so enqueued jobs append rather than
/// splitting pages across the random GUID key.
/// </para>
/// </remarks>
internal sealed class IntegrationOutboxConfiguration : IEntityTypeConfiguration<IntegrationOutbox>
{
    public void Configure(EntityTypeBuilder<IntegrationOutbox> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("IntegrationOutbox");

        builder.Property(job => job.OperationKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(job => job.PayloadReference)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(job => job.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(OutboxStatus.Pending);

        builder.Property(job => job.LeaseOwner)
            .HasMaxLength(128);

        builder.Property(job => job.LeaseExpiresAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(job => job.OperationKey)
            .IsUnique();

        builder.HasIndex(job => new { job.ConnectionId, job.Status, job.LeaseExpiresAtUtc });

        // Non-clustered GUID PK + clustered key on the insert-ordered CreatedAtUtc column to
        // avoid random-GUID page-split fragmentation on this hot insert-heavy table.
        builder.HasKey(job => job.Id).IsClustered(false);
        builder.HasIndex(job => job.CreatedAtUtc).IsClustered();

        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(job => job.ConnectionId)
            .IsRequired();
    }
}
