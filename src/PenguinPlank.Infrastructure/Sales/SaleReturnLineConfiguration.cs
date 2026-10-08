using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Sales;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Sales;

/// <summary>
/// EF Core mapping for the designed-only <see cref="SaleReturnLine"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The return-header FK is typed and Restrict-deleted. The
/// original sale-line reference is a designed-only scalar seam (no FK in Phase A). The refund
/// uses money precision.
/// </remarks>
internal sealed class SaleReturnLineConfiguration : IEntityTypeConfiguration<SaleReturnLine>
{
    public void Configure(EntityTypeBuilder<SaleReturnLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SaleReturnLines");

        builder.Property(line => line.RefundAmount)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.Property(line => line.RestockDisposition)
            .HasMaxLength(32);

        builder.HasOne<SaleReturn>()
            .WithMany()
            .HasForeignKey(line => line.SaleReturnId)
            .IsRequired();
    }
}
