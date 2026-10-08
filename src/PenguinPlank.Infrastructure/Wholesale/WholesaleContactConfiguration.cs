using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Wholesale;

namespace PenguinPlank.Infrastructure.Wholesale;

/// <summary>
/// EF Core mapping for the designed-only <see cref="WholesaleContact"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The optional account association
/// (<see cref="WholesaleContact.WholesaleAccountId"/>) is a designed-only scalar seam (no FK
/// in Phase A) so a contact can exist independently of any company.
/// </remarks>
internal sealed class WholesaleContactConfiguration : IEntityTypeConfiguration<WholesaleContact>
{
    public void Configure(EntityTypeBuilder<WholesaleContact> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WholesaleContacts");

        builder.Property(contact => contact.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(contact => contact.Email)
            .HasMaxLength(256);

        builder.Property(contact => contact.Phone)
            .HasMaxLength(64);

        builder.Property(contact => contact.ActiveFlag)
            .HasDefaultValue(true);
    }
}
