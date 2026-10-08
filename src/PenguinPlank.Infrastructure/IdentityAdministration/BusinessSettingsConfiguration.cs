using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.IdentityAdministration;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.IdentityAdministration;

/// <summary>
/// EF Core mapping for the single-row <see cref="BusinessSettings"/>.
/// </summary>
/// <remarks>
/// Business-wide configuration (A2 5.3): the timezone defaults to <c>America/Los_Angeles</c>, the
/// currency to <c>USD</c>, and the media size-limit warning thresholds to 20 MB (image) and 200 MB
/// (video) (requirement 6.10 / A4). Default rates use the rate precision (decimal(19,6)) rather
/// than the model-wide money default. "Single row" is enforced by the settings use case added by a
/// later task (one well-known record); this configuration maps the stored shape and defaults.
/// </remarks>
internal sealed class BusinessSettingsConfiguration : IEntityTypeConfiguration<BusinessSettings>
{
    public void Configure(EntityTypeBuilder<BusinessSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BusinessSettings");

        builder.Property(settings => settings.Timezone)
            .IsRequired()
            .HasMaxLength(64)
            .HasDefaultValue("America/Los_Angeles");

        builder.Property(settings => settings.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(settings => settings.DefaultLaborRate)
            .HasPrecision(DecimalPrecision.RatePrecision, DecimalPrecision.RateScale);

        builder.Property(settings => settings.DefaultOverheadRate)
            .HasPrecision(DecimalPrecision.RatePrecision, DecimalPrecision.RateScale);

        builder.Property(settings => settings.DefaultDimensionUnit)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue("in");

        builder.Property(settings => settings.ImageSizeLimitBytes)
            .HasDefaultValue(20L * 1024 * 1024);

        builder.Property(settings => settings.VideoSizeLimitBytes)
            .HasDefaultValue(200L * 1024 * 1024);
    }
}
