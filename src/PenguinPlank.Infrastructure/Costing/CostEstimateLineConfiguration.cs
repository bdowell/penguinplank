using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Costing;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Costing;

/// <summary>
/// EF Core mapping for the designed-only <see cref="CostEstimateLine"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The estimate FK is typed and Restrict-deleted so costing
/// history is preserved (design invariant 5). <see cref="CostEstimateLine.Quantity"/> is a
/// fractional costing quantity (dimension precision), <see cref="CostEstimateLine.Rate"/>
/// uses rate precision, and <see cref="CostEstimateLine.LineCost"/> uses money precision.
/// </remarks>
internal sealed class CostEstimateLineConfiguration : IEntityTypeConfiguration<CostEstimateLine>
{
    public void Configure(EntityTypeBuilder<CostEstimateLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CostEstimateLines");

        builder.Property(line => line.Description)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(line => line.Quantity)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);

        builder.Property(line => line.Rate)
            .HasPrecision(DecimalPrecision.RatePrecision, DecimalPrecision.RateScale);

        builder.Property(line => line.LineCost)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.HasOne<CostEstimate>()
            .WithMany()
            .HasForeignKey(line => line.CostEstimateId)
            .IsRequired();
    }
}
