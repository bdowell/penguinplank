using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="ProductPiece"/>.
/// </summary>
/// <remarks>
/// <para>
/// Adds the piece-specific constraints: the <b>filtered unique index</b> on
/// <see cref="ProductPiece.PieceCode"/> that ignores nulls (requirement 1.7), per-piece
/// dimension columns at <c>decimal(12,4)</c>, the owning-variant and produced-with
/// care-version foreign keys, and the <see cref="PublicationState.Draft"/> database default
/// (requirements 1.12, 2.6, 2.7).
/// </para>
/// <para>
/// <b>Designed-only production link.</b> <see cref="ProductPiece.ProductionBatchLineId"/> is
/// mapped as a plain nullable GUID column with <b>no foreign key and no navigation</b>: the
/// Production table does not exist in Phase A, so no referential constraint is declared. The
/// column reserves the seam for an additive FK when Production is implemented, without forcing
/// a schema replacement later (design "additive extension seams"). It is deliberately left out
/// of all relationship configuration so EF maps it as an ordinary scalar.
/// </para>
/// </remarks>
internal sealed class ProductPieceConfiguration : IEntityTypeConfiguration<ProductPiece>
{
    public void Configure(EntityTypeBuilder<ProductPiece> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductPieces");

        builder.Property(piece => piece.PieceCode)
            .HasMaxLength(64);

        builder.Property(piece => piece.DimensionUnit)
            .HasMaxLength(16);

        builder.Property(piece => piece.Finish)
            .HasMaxLength(200);

        builder.Property(piece => piece.Story)
            .HasMaxLength(4000);

        builder.Property(piece => piece.Status)
            .HasMaxLength(64);

        builder.Property(piece => piece.ProductionDate)
            .HasColumnType("datetimeoffset");

        // Per-piece actual dimensions, decimal(12,4), stored independently of the variant.
        builder.Property(piece => piece.Length)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);
        builder.Property(piece => piece.Width)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);
        builder.Property(piece => piece.Thickness)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);
        builder.Property(piece => piece.Diameter)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);

        builder.Property(piece => piece.PublicationState)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(PublicationState.Draft);

        builder.Property(piece => piece.ActiveFlag)
            .HasDefaultValue(true);

        // Piece code is unique only when supplied (requirement 1.7).
        builder.HasIndex(piece => piece.PieceCode)
            .IsUnique()
            .HasFilter("[PieceCode] IS NOT NULL");

        // Owning serialized variant (typed FK, Restrict delete).
        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(piece => piece.VariantId)
            .IsRequired();

        // Produced-with care version: optional typed FK to the immutable version in effect at
        // production time (requirement 2.4).
        builder.HasOne<CareProfileVersion>()
            .WithMany()
            .HasForeignKey(piece => piece.CareProfileVersionId)
            .IsRequired(false);

        // ProductionBatchLineId is intentionally NOT configured as a relationship: it is a
        // designed-only nullable scalar column awaiting the future Production table.
    }
}
