using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Costing;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Costing;

/// <summary>
/// EF Core mapping for the designed-only <see cref="CostEstimate"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The variant/piece scope references are designed-only
/// scalar seams (no FK in Phase A). Monetary columns use money precision.
/// </remarks>
internal sealed class CostEstimateConfiguration : IEntityTypeConfiguration<CostEstimate>
{
    public void Configure(EntityTypeBuilder<CostEstimate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CostEstimates");

        builder.Property(estimate => estimate.LaborCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(estimate => estimate.WasteCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(estimate => estimate.OverheadCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(estimate => estimate.TotalCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
    }
}
