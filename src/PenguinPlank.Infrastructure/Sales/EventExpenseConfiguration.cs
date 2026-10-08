using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Sales;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Sales;

/// <summary>
/// EF Core mapping for the designed-only <see cref="EventExpense"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The event and receipt-media references are designed-only
/// scalar seams (no FK in Phase A). The amount uses money precision; work hours use dimension
/// precision.
/// </remarks>
internal sealed class EventExpenseConfiguration : IEntityTypeConfiguration<EventExpense>
{
    public void Configure(EntityTypeBuilder<EventExpense> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EventExpenses");

        builder.Property(expense => expense.Category)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(expense => expense.Amount)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.Property(expense => expense.WorkHours)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);

        builder.HasIndex(expense => expense.EventId);
    }
}
