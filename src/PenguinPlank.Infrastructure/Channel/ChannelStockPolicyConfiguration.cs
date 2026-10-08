using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Channel;

namespace PenguinPlank.Infrastructure.Channel;

/// <summary>
/// EF Core mapping for the designed-only <see cref="ChannelStockPolicy"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The channel/connection reference is a designed-only scalar
/// seam (no FK in Phase A).
/// </remarks>
internal sealed class ChannelStockPolicyConfiguration : IEntityTypeConfiguration<ChannelStockPolicy>
{
    public void Configure(EntityTypeBuilder<ChannelStockPolicy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ChannelStockPolicies");

        builder.Property(policy => policy.AuthorityPolicy)
            .HasMaxLength(64);

        builder.HasIndex(policy => policy.ChannelId);
    }
}
