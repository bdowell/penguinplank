using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Sales;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Sales;

/// <summary>
/// EF Core mapping for the designed-only <see cref="Sale"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). All monetary columns use money precision. The
/// event/contact/customer/movement references — including the <see cref="Sale.ContactId"/>
/// and <see cref="Sale.CustomerId"/> customer-foundation extension seams — are designed-only
/// scalar columns with <b>no FK constraint</b> in Phase A, reserving additive links without
/// creating a CustomerIdentity table. A sale-date index supports date lookups.
/// </remarks>
internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Sales");

        builder.Property(sale => sale.Channel)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(sale => sale.ExternalReference)
            .HasMaxLength(200);

        builder.Property(sale => sale.SaleDate)
            .HasColumnType("datetimeoffset");

        builder.Property(sale => sale.Subtotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(sale => sale.DiscountTotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(sale => sale.TaxTotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(sale => sale.FeeTotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(sale => sale.GrandTotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(sale => sale.CostSnapshot)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.HasIndex(sale => sale.SaleDate);
    }
}
