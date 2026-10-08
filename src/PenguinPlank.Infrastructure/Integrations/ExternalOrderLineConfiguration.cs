using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="ExternalOrderLine"/>.
/// </summary>
/// <remarks>
/// A normalized line of a captured order. The optional <see cref="ExternalOrderLine.VariantId"/>
/// is populated only once the line's external resource is mapped to an internal variant; it stays
/// null while unresolved (matches are reviewed, never applied silently — requirement 3.5). The
/// monetary <see cref="ExternalOrderLine.UnitPrice"/> uses the model-wide money precision
/// (decimal(19,4)).
/// </remarks>
internal sealed class ExternalOrderLineConfiguration : IEntityTypeConfiguration<ExternalOrderLine>
{
    public void Configure(EntityTypeBuilder<ExternalOrderLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ExternalOrderLines");

        builder.Property(line => line.ExternalLineId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(line => line.UnitPrice)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.HasIndex(line => line.ExternalOrderId);

        builder.HasOne<ExternalOrder>()
            .WithMany()
            .HasForeignKey(line => line.ExternalOrderId)
            .IsRequired();
    }
}
