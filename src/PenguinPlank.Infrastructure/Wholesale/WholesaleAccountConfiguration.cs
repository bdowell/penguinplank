using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Wholesale;

namespace PenguinPlank.Infrastructure.Wholesale;

/// <summary>
/// EF Core mapping for the designed-only <see cref="WholesaleAccount"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4).
/// </remarks>
internal sealed class WholesaleAccountConfiguration : IEntityTypeConfiguration<WholesaleAccount>
{
    public void Configure(EntityTypeBuilder<WholesaleAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WholesaleAccounts");

        builder.Property(account => account.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(account => account.Stage)
            .HasMaxLength(32);

        builder.Property(account => account.InternalNotes)
            .HasMaxLength(4000);

        builder.Property(account => account.ActiveFlag)
            .HasDefaultValue(true);
    }
}
