using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Sales;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Sales;

/// <summary>
/// EF Core mapping for the designed-only <see cref="EventReconciliation"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The event reference is a designed-only scalar seam (no FK
/// in Phase A). Monetary columns use money precision.
/// </remarks>
internal sealed class EventReconciliationConfiguration : IEntityTypeConfiguration<EventReconciliation>
{
    public void Configure(EntityTypeBuilder<EventReconciliation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventReconciliations");

        builder.Property(reconciliation => reconciliation.CountedTotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(reconciliation => reconciliation.ExpectedTotal)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(reconciliation => reconciliation.Variance)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.Property(reconciliation => reconciliation.ClosureResolution)
            .HasMaxLength(2000);

        builder.HasIndex(reconciliation => reconciliation.EventId);
    }
}
