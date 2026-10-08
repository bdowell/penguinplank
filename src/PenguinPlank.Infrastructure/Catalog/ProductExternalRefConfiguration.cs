using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="ProductExternalRef"/>.
/// </summary>
/// <remarks>
/// A typed external reference owned by a <see cref="Product"/> (requirement 6.13 / design
/// invariant 8). The composite <b>unique index</b> on
/// (<see cref="ProductExternalRef.Platform"/>, <see cref="ProductExternalRef.ProductId"/>,
/// <see cref="ProductExternalRef.ExternalId"/>) enforces one reference per platform/owner/
/// external-id triple.
/// </remarks>
internal sealed class ProductExternalRefConfiguration : IEntityTypeConfiguration<ProductExternalRef>
{
    public void Configure(EntityTypeBuilder<ProductExternalRef> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductExternalRefs");

        builder.Property(reference => reference.Platform)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(reference => reference.ExternalId)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(reference => new { reference.Platform, reference.ProductId, reference.ExternalId })
            .IsUnique();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(reference => reference.ProductId)
            .IsRequired();
    }
}
