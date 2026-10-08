using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="IntegrationInbox"/>.
/// </summary>
/// <remarks>
/// The <b>unique index on <see cref="IntegrationInbox.DeliveryKey"/></b> deduplicates repeated or
/// out-of-order inbound deliveries so they reconcile authoritative state rather than reapply
/// changes (requirement 3.8 / R11; design index strategy). The covering lookup index on
/// (<see cref="IntegrationInbox.ConnectionId"/>, <see cref="IntegrationInbox.Status"/>,
/// <see cref="IntegrationInbox.LeaseExpiresAtUtc"/>) supports lease-based worker polling.
/// <see cref="IntegrationInbox.Status"/> is stored as a readable string with a
/// <see cref="InboxStatus.Received"/> default.
/// <para>
/// <b>GUID-key fragmentation strategy (design index strategy).</b> The inbox is a hot,
/// insert-heavy table. The primary key is made <em>non-clustered</em> and the clustered key is
/// placed on the insert-ordered <c>CreatedAtUtc</c> column so new deliveries append rather than
/// splitting pages across the random GUID key.
/// </para>
/// </remarks>
internal sealed class IntegrationInboxConfiguration : IEntityTypeConfiguration<IntegrationInbox>
{
    public void Configure(EntityTypeBuilder<IntegrationInbox> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("IntegrationInbox");

        builder.Property(item => item.DeliveryKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(item => item.PayloadReference)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(item => item.SignatureState)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(InboxStatus.Received);

        builder.Property(item => item.LeaseOwner)
            .HasMaxLength(128);

        builder.Property(item => item.LeaseExpiresAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(item => item.ReceivedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(item => item.DeliveryKey)
            .IsUnique();

        builder.HasIndex(item => new { item.ConnectionId, item.Status, item.LeaseExpiresAtUtc });

        // Non-clustered GUID PK + clustered key on the insert-ordered CreatedAtUtc column to
        // avoid random-GUID page-split fragmentation on this hot insert-heavy table.
        builder.HasKey(item => item.Id).IsClustered(false);
        builder.HasIndex(item => item.CreatedAtUtc).IsClustered();

        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(item => item.ConnectionId)
            .IsRequired();
    }
}
