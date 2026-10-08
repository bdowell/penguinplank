using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for <see cref="CareProfile"/>.
/// </summary>
/// <remarks>
/// The care profile is the stable identity owning one or more immutable
/// <see cref="CareProfileVersion"/> records (requirement 2.3). GUID key and timestamps come
/// from the base entity convention; this adds the required name and active-flag defaults.
/// </remarks>
internal sealed class CareProfileConfiguration : IEntityTypeConfiguration<CareProfile>
{
    public void Configure(EntityTypeBuilder<CareProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CareProfiles");

        builder.Property(profile => profile.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(profile => profile.ActiveFlag)
            .HasDefaultValue(true);
    }
}
