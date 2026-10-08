using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Production;

namespace PenguinPlank.Infrastructure.Production;

/// <summary>
/// EF Core mapping for the designed-only <see cref="ProductionBatchLine"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The batch FK is typed and Restrict-deleted so production
/// history is preserved. <see cref="ProductionBatchLine.CurrentStageId"/> is left an ordinary
/// nullable scalar (no relationship) to keep the mapping minimal. This is the row the Catalog
/// <c>ProductPiece.ProductionBatchLineId</c> seam will reference additively in a later phase;
/// Catalog is intentionally left unmodified in Phase A so that seam stays a documented scalar.
/// </remarks>
internal sealed class ProductionBatchLineConfiguration : IEntityTypeConfiguration<ProductionBatchLine>
{
    public void Configure(EntityTypeBuilder<ProductionBatchLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductionBatchLines");

        builder.Property(line => line.Disposition)
            .HasMaxLength(64);

        builder.HasOne<ProductionBatch>()
            .WithMany()
            .HasForeignKey(line => line.ProductionBatchId)
            .IsRequired();
    }
}
