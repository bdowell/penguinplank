using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="ProductVariant"/>.
/// </summary>
/// <remarks>
/// Adds the variant-specific constraints on top of the base entity convention: the required
/// <see cref="ProductVariant.Sku"/> <b>unique index</b> and the <b>filtered unique index</b>
/// on <see cref="ProductVariant.Barcode"/> that ignores nulls (requirements 1.1, 1.2; design
/// invariant 1), dimension columns at <c>decimal(12,4)</c> (overriding the model-wide money
/// default), money columns at <c>decimal(19,4)</c>, the <see cref="TrackingMode"/> enum
/// conversion, and the owning-product foreign key. A covering lookup index on
/// (<see cref="ProductVariant.ProductId"/>, <see cref="ProductVariant.ActiveFlag"/>) supports
/// the hot "variants for a product" query (design index strategy).
/// </remarks>
internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProductVariants");

        builder.Property(variant => variant.Sku)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(variant => variant.Barcode)
            .HasMaxLength(64);

        builder.Property(variant => variant.UnitOfMeasure)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(variant => variant.TrackingMode)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(variant => variant.DimensionUnit)
            .HasMaxLength(16);

        builder.Property(variant => variant.Finish)
            .HasMaxLength(200);

        // Dimensions are decimal(12,4), overriding the model-wide money default.
        builder.Property(variant => variant.Length)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);
        builder.Property(variant => variant.Width)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);
        builder.Property(variant => variant.Thickness)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);
        builder.Property(variant => variant.Diameter)
            .HasPrecision(DecimalPrecision.DimensionPrecision, DecimalPrecision.DimensionScale);

        // Money is decimal(19,4) (the model-wide default, restated here for clarity).
        builder.Property(variant => variant.RetailPrice)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);
        builder.Property(variant => variant.WholesalePrice)
            .HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale);

        builder.Property(variant => variant.ActiveFlag)
            .HasDefaultValue(true);

        // SKU is unique across all variants (requirements 1.1, 1.2).
        builder.HasIndex(variant => variant.Sku)
            .IsUnique();

        // Barcode is unique only when supplied: a filtered index ignoring NULLs so many
        // variants may have no barcode (design invariant 1).
        builder.HasIndex(variant => variant.Barcode)
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL");

        // Hot lookup: variants belonging to a product, filtered by active state.
        builder.HasIndex(variant => new { variant.ProductId, variant.ActiveFlag });

        // Owning product (typed FK; no cascade by the model-wide Restrict default).
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(variant => variant.ProductId)
            .IsRequired();

        // Optional care-profile association.
        builder.HasOne<CareProfile>()
            .WithMany()
            .HasForeignKey(variant => variant.CareProfileId)
            .IsRequired(false);
    }
}
