using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="ExternalOrder"/>.
/// </summary>
/// <remarks>
/// Captured-order schema (design entity catalog): modeled and stored in Phase A, posting logic
/// designed-only. A unique index on
/// (<see cref="ExternalOrder.Platform"/>, <see cref="ExternalOrder.Account"/>,
/// <see cref="ExternalOrder.ExternalId"/>) keeps a captured order's mirrored identity distinct.
/// Lines and reservations are never cascade-deleted (model-wide restrict default).
/// </remarks>
internal sealed class ExternalOrderConfiguration : IEntityTypeConfiguration<ExternalOrder>
{
    public void Configure(EntityTypeBuilder<ExternalOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ExternalOrders");

        builder.Property(order => order.Platform)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(order => order.Account)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(order => order.ExternalId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(order => order.PaymentState)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(order => order.FulfillmentState)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(order => order.PostingStatus)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(order => new { order.Platform, order.Account, order.ExternalId })
            .IsUnique();

        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(order => order.ConnectionId)
            .IsRequired();
    }
}
