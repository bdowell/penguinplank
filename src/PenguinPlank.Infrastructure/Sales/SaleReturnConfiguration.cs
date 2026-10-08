using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Sales;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Sales;

/// <summary>
/// EF Core mapping for the designed-only <see cref="SaleReturn"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The original-sale FK is typed and Restrict-deleted so
/// sale/return history is preserved. The compensating-movement reference is a designed-only
/// scalar seam (no FK in Phase A). The refund uses money precision.
/// </remarks>
internal sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SaleReturns");

        builder.Property(saleReturn => saleReturn.ReturnDate)
            .HasColumnType("datetimeoffset");

        builder.Property(saleReturn => saleReturn.RefundTotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.HasOne<Sale>()
            .WithMany()
            .HasForeignKey(saleReturn => saleReturn.SaleId)
            .IsRequired();
    }
}
