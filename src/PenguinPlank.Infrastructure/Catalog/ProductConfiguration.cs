using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="Product"/>.
/// </summary>
/// <remarks>
/// The GUID key, <c>datetimeoffset</c> timestamps, and <c>rowversion</c> concurrency token
/// come from the base entity convention in <c>PenguinPlankDbContext</c>; this configuration
/// adds only what is Product-specific: required string columns, the public-ready vs. internal
/// column separation, and the <see cref="PublicationState.Draft"/> database default so a
/// product cannot be inserted publicly visible (requirements 1.11, 1.12).
/// </remarks>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Products");

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(product => product.Category)
            .HasMaxLength(200);

        // Public-ready field, stored separately from InternalNotes (requirement 1.11).
        builder.Property(product => product.PublicDescription)
            .HasMaxLength(4000);

        // Internal field, never exposed publicly; a distinct column from PublicDescription.
        builder.Property(product => product.InternalNotes)
            .HasMaxLength(4000);

        // Persist the enum by its stable name and default to Draft at the database level
        // (requirement 1.12) in addition to the POCO default.
        builder.Property(product => product.PublicationState)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(PublicationState.Draft);

        builder.Property(product => product.ActiveFlag)
            .HasDefaultValue(true);
    }
}
