using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Inventory;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Inventory;

/// <summary>
/// EF Core mapping for the designed-only <see cref="InventoryBalance"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The balance is unique per
/// (<see cref="InventoryBalance.VariantId"/>, <see cref="InventoryBalance.LocationId"/>) —
/// the design's stock <c>(VariantId, LocationId)</c> index, declared unique here. The variant
/// link is a designed-only scalar seam (no FK); the location FK is typed and Restrict-deleted.
/// Cost components use money precision.
/// </remarks>
internal sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("InventoryBalances");

        builder.Property(balance => balance.UnitCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.Property(balance => balance.TotalCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(balance => balance.LocationId)
            .IsRequired();

        builder.HasIndex(balance => new { balance.VariantId, balance.LocationId })
            .IsUnique();
    }
}
