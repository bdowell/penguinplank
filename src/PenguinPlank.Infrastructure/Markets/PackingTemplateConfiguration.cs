using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Markets;

namespace PenguinPlank.Infrastructure.Markets;

/// <summary>
/// EF Core mapping for the designed-only <see cref="PackingTemplate"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4).
/// </remarks>
internal sealed class PackingTemplateConfiguration : IEntityTypeConfiguration<PackingTemplate>
{
    public void Configure(EntityTypeBuilder<PackingTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PackingTemplates");

        builder.Property(template => template.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(template => template.Description)
            .HasMaxLength(2000);

        builder.Property(template => template.ActiveFlag)
            .HasDefaultValue(true);
    }
}
