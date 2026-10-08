using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for the <see cref="VariantWood"/> composition join.
/// </summary>
/// <remarks>
/// This entity does not derive from <c>Entity</c>, so the base entity convention does not
/// apply: the <b>composite primary key</b> (<see cref="VariantWood.VariantId"/>,
/// <see cref="VariantWood.WoodSpeciesId"/>) and both foreign keys are configured here. The
/// composite key itself prevents the same species being linked twice to one variant, so a
/// variant may be composed of multiple distinct species (requirements 1.5, 2.2).
/// <see cref="VariantWood.Proportion"/> is a nullable percentage with modest precision.
/// </remarks>
internal sealed class VariantWoodConfiguration : IEntityTypeConfiguration<VariantWood>
{
    public void Configure(EntityTypeBuilder<VariantWood> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("VariantWoods");

        builder.HasKey(link => new { link.VariantId, link.WoodSpeciesId });

        // Proportion is a 0–100 percentage; precision(5,2) covers 100.00 with headroom.
        builder.Property(link => link.Proportion)
            .HasPrecision(5, 2);

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(link => link.VariantId)
            .IsRequired();

        builder.HasOne<WoodSpecies>()
            .WithMany()
            .HasForeignKey(link => link.WoodSpeciesId)
            .IsRequired();
    }
}
