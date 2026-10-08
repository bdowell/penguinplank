using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for the <see cref="PieceWood"/> composition join.
/// </summary>
/// <remarks>
/// Mirrors <see cref="VariantWoodConfiguration"/> for individual pieces: a <b>composite primary
/// key</b> (<see cref="PieceWood.PieceId"/>, <see cref="PieceWood.WoodSpeciesId"/>) and both
/// typed foreign keys, so a piece may be composed of multiple distinct wood species
/// (requirements 1.5, 2.2). <see cref="PieceWood.Proportion"/> is a nullable percentage.
/// </remarks>
internal sealed class PieceWoodConfiguration : IEntityTypeConfiguration<PieceWood>
{
    public void Configure(EntityTypeBuilder<PieceWood> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PieceWoods");

        builder.HasKey(link => new { link.PieceId, link.WoodSpeciesId });

        builder.Property(link => link.Proportion)
            .HasPrecision(5, 2);

        builder.HasOne<ProductPiece>()
            .WithMany()
            .HasForeignKey(link => link.PieceId)
            .IsRequired();

        builder.HasOne<WoodSpecies>()
            .WithMany()
            .HasForeignKey(link => link.WoodSpeciesId)
            .IsRequired();
    }
}
