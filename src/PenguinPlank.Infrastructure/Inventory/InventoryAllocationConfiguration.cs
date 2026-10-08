using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Inventory;

namespace PenguinPlank.Infrastructure.Inventory;

/// <summary>
/// EF Core mapping for the designed-only <see cref="InventoryAllocation"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The event and variant/piece references are designed-only
/// scalar seams with no FK in Phase A.
/// </remarks>
internal sealed class InventoryAllocationConfiguration : IEntityTypeConfiguration<InventoryAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryAllocation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("InventoryAllocations");

        builder.Property(allocation => allocation.State)
            .HasMaxLength(32);

        builder.HasIndex(allocation => allocation.EventId);
    }
}
