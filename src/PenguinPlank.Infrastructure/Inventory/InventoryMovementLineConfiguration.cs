using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Inventory;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Inventory;

/// <summary>
/// EF Core mapping for the designed-only, append-only <see cref="InventoryMovementLine"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The movement-header FK is typed and Restrict-deleted so
/// ledger history survives. The variant/piece references are designed-only scalar seams (no
/// FK in Phase A). The carried cost uses money precision.
/// </remarks>
internal sealed class InventoryMovementLineConfiguration : IEntityTypeConfiguration<InventoryMovementLine>
{
    public void Configure(EntityTypeBuilder<InventoryMovementLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("InventoryMovementLines");

        builder.Property(line => line.CarriedCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.HasOne<InventoryMovement>()
            .WithMany()
            .HasForeignKey(line => line.InventoryMovementId)
            .IsRequired();
    }
}
