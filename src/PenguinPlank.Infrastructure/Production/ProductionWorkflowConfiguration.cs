using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Production;

namespace PenguinPlank.Infrastructure.Production;

/// <summary>
/// EF Core mapping for the designed-only <see cref="ProductionWorkflow"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4): the table is modeled and migrated, but no services use
/// it. The GUID key, timestamps, and <c>rowversion</c> come from the base entity convention.
/// </remarks>
internal sealed class ProductionWorkflowConfiguration : IEntityTypeConfiguration<ProductionWorkflow>
{
    public void Configure(EntityTypeBuilder<ProductionWorkflow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductionWorkflows");

        builder.Property(workflow => workflow.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(workflow => workflow.Description)
            .HasMaxLength(2000);

        builder.Property(workflow => workflow.ActiveFlag)
            .HasDefaultValue(true);
    }
}
