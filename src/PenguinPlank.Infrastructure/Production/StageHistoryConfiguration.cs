using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Production;

namespace PenguinPlank.Infrastructure.Production;

/// <summary>
/// EF Core mapping for the designed-only, append-only <see cref="StageHistory"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). Stage history is append-only; both parent FKs
/// (<see cref="ProductionBatchLine"/> and <see cref="WorkflowStage"/>) use the model-wide
/// Restrict delete behavior so production history is never destroyed by a parent delete
/// (design invariant 5).
/// </remarks>
internal sealed class StageHistoryConfiguration : IEntityTypeConfiguration<StageHistory>
{
    public void Configure(EntityTypeBuilder<StageHistory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StageHistories");

        builder.Property(history => history.EnteredAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(history => history.ExitedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(history => history.Disposition)
            .HasMaxLength(200);

        builder.HasOne<ProductionBatchLine>()
            .WithMany()
            .HasForeignKey(history => history.ProductionBatchLineId)
            .IsRequired();

        builder.HasOne<WorkflowStage>()
            .WithMany()
            .HasForeignKey(history => history.WorkflowStageId)
            .IsRequired();
    }
}
