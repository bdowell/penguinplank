using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Channel;

namespace PenguinPlank.Infrastructure.Channel;

/// <summary>
/// EF Core mapping for the designed-only <see cref="ChannelAllocation"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The policy FK is typed and Restrict-deleted. The
/// variant/piece references are designed-only scalar seams (no FK in Phase A).
/// </remarks>
internal sealed class ChannelAllocationConfiguration : IEntityTypeConfiguration<ChannelAllocation>
{
    public void Configure(EntityTypeBuilder<ChannelAllocation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ChannelAllocations");

        builder.HasOne<ChannelStockPolicy>()
            .WithMany()
            .HasForeignKey(allocation => allocation.ChannelStockPolicyId)
            .IsRequired();
    }
}
