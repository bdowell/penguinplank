using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Sales;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Sales;

/// <summary>
/// EF Core mapping for the designed-only <see cref="SaleLine"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The sale-header FK is typed and Restrict-deleted so sale
/// history is preserved (design invariant 5). The variant/piece references are designed-only
/// scalar seams (no FK in Phase A). Monetary columns use money precision.
/// </remarks>
internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SaleLines");

        builder.Property(line => line.UnitPrice)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(line => line.LineDiscount)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(line => line.CostSnapshot)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(line => line.SaleId)
            .IsRequired();
    }
}
