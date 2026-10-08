using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="VariantExternalRef"/>.
/// </summary>
/// <remarks>
/// The variant-typed sibling of <see cref="ProductExternalRefConfiguration"/>. The composite
/// <b>unique index</b> on (<see cref="VariantExternalRef.Platform"/>,
/// <see cref="VariantExternalRef.VariantId"/>, <see cref="VariantExternalRef.ExternalId"/>)
/// enforces one reference per platform/owner/external-id triple (requirement 6.13 / design
/// invariant 8).
/// </remarks>
internal sealed class VariantExternalRefConfiguration : IEntityTypeConfiguration<VariantExternalRef>
{
    public void Configure(EntityTypeBuilder<VariantExternalRef> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("VariantExternalRefs");

        builder.Property(reference => reference.Platform)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(reference => reference.ExternalId)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(reference => new { reference.Platform, reference.VariantId, reference.ExternalId })
            .IsUnique();

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(reference => reference.VariantId)
            .IsRequired();
    }
}
