using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Production;

namespace PenguinPlank.Infrastructure.Production;

/// <summary>
/// EF Core mapping for the designed-only <see cref="WorkflowStage"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). A stage belongs to one <see cref="ProductionWorkflow"/>
/// via a typed FK that uses the model-wide Restrict delete behavior.
/// </remarks>
internal sealed class WorkflowStageConfiguration : IEntityTypeConfiguration<WorkflowStage>
{
    public void Configure(EntityTypeBuilder<WorkflowStage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WorkflowStages");

        builder.Property(stage => stage.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne<ProductionWorkflow>()
            .WithMany()
            .HasForeignKey(stage => stage.ProductionWorkflowId)
            .IsRequired();

        builder.HasIndex(stage => new { stage.ProductionWorkflowId, stage.SequenceOrder });
    }
}
