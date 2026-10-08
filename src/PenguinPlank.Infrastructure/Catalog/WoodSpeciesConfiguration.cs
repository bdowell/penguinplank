using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="WoodSpecies"/> reference data.
/// </summary>
/// <remarks>
/// The GUID key and timestamps come from the base entity convention. A unique index on
/// <see cref="WoodSpecies.Name"/> keeps the configurable species list free of duplicates.
/// </remarks>
internal sealed class WoodSpeciesConfiguration : IEntityTypeConfiguration<WoodSpecies>
{
    public void Configure(EntityTypeBuilder<WoodSpecies> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WoodSpecies");

        builder.Property(species => species.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(species => species.ActiveFlag)
            .HasDefaultValue(true);

        builder.HasIndex(species => species.Name)
            .IsUnique();
    }
}
