using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Wholesale;

namespace PenguinPlank.Infrastructure.Wholesale;

/// <summary>
/// EF Core mapping for the designed-only <see cref="Interaction"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The account/contact/assignee references are designed-only
/// scalar seams (no FK in Phase A).
/// </remarks>
internal sealed class InteractionConfiguration : IEntityTypeConfiguration<Interaction>
{
    public void Configure(EntityTypeBuilder<Interaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Interactions");

        builder.Property(interaction => interaction.InteractionType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(interaction => interaction.Summary)
            .HasMaxLength(4000);

        builder.Property(interaction => interaction.InteractionDate)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(interaction => interaction.InteractionDate);
    }
}
