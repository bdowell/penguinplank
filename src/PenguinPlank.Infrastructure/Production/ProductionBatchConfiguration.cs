using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Production;

namespace PenguinPlank.Infrastructure.Production;

/// <summary>
/// EF Core mapping for the designed-only <see cref="ProductionBatch"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The workflow FK is typed and Restrict-deleted. The
/// variant link and the <see cref="ProductionBatch.ParentBatchId"/> sublot self-link are
/// designed-only scalar seams with <b>no FK constraint</b> in Phase A. The target-date index
/// supports the design's stage/due-date lookups.
/// </remarks>
internal sealed class ProductionBatchConfiguration : IEntityTypeConfiguration<ProductionBatch>
{
    public void Configure(EntityTypeBuilder<ProductionBatch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductionBatches");

        builder.Property(batch => batch.Status)
            .HasMaxLength(64);

        builder.Property(batch => batch.WorkflowSnapshot)
            .HasMaxLength(4000);

        builder.Property(batch => batch.TargetDate)
            .HasColumnType("datetimeoffset");

        builder.HasOne<ProductionWorkflow>()
            .WithMany()
            .HasForeignKey(batch => batch.ProductionWorkflowId)
            .IsRequired();

        builder.HasIndex(batch => batch.TargetDate);
    }
}
