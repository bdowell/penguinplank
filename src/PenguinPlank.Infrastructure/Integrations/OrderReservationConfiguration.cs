using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="OrderReservation"/>.
/// </summary>
/// <remarks>
/// A stock reservation held against a captured order (design entity catalog): schema stored in
/// Phase A, conversion-to-allocation logic designed-only. A lookup index on
/// <see cref="OrderReservation.ExternalOrderId"/> supports listing an order's reservations.
/// </remarks>
internal sealed class OrderReservationConfiguration : IEntityTypeConfiguration<OrderReservation>
{
    public void Configure(EntityTypeBuilder<OrderReservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OrderReservations");

        builder.Property(reservation => reservation.State)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(reservation => reservation.ExternalOrderId);

        builder.HasOne<ExternalOrder>()
            .WithMany()
            .HasForeignKey(reservation => reservation.ExternalOrderId)
            .IsRequired();
    }
}
