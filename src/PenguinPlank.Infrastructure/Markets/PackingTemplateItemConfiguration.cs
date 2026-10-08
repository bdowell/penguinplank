using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Markets;

namespace PenguinPlank.Infrastructure.Markets;

/// <summary>
/// EF Core mapping for the designed-only <see cref="PackingTemplateItem"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The template FK is typed and Restrict-deleted. The
/// referenced-entity link is a designed-only scalar seam (no FK in Phase A).
/// </remarks>
internal sealed class PackingTemplateItemConfiguration : IEntityTypeConfiguration<PackingTemplateItem>
{
    public void Configure(EntityTypeBuilder<PackingTemplateItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PackingTemplateItems");

        builder.Property(item => item.ReferenceType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(item => item.Instructions)
            .HasMaxLength(2000);

        builder.HasOne<PackingTemplate>()
            .WithMany()
            .HasForeignKey(item => item.PackingTemplateId)
            .IsRequired();
    }
}
