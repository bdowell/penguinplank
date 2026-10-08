using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Inventory;

namespace PenguinPlank.Infrastructure.Inventory;

/// <summary>
/// EF Core mapping for the designed-only, append-only <see cref="InventoryMovement"/> header.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The movement ledger is append-only. The source and
/// destination location links and the related-sale link are designed-only scalar seams with
/// <b>no FK constraint</b> in Phase A, so a configured FK does not force a dependency on tables
/// that may belong to other modules. All relationships (when later wired) keep the Restrict
/// default so ledger history is never cascade-deleted (design invariant 5).
/// </remarks>
internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("InventoryMovements");

        builder.Property(movement => movement.MovementType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(movement => movement.MovementDate)
            .HasColumnType("datetimeoffset");

        builder.Property(movement => movement.Reason)
            .HasMaxLength(1000);

        builder.HasIndex(movement => movement.MovementDate);
    }
}
