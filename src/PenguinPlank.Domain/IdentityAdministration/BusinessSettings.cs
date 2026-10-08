using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.IdentityAdministration;

/// <summary>
/// The single-row record of business-wide settings: timezone, default rates, units, currency, and
/// the media size-limit warning thresholds.
/// </summary>
/// <remarks>
/// <para>
/// Business settings are a single configuration row for the whole application. The defaults
/// reflect the business: a <see cref="Timezone"/> of <c>America/Los_Angeles</c> (used for
/// date-only local-date resolution including DST), a <see cref="Currency"/> of <c>USD</c>, and
/// size-limit warning thresholds of 20 MB for images and 200 MB for videos (requirement 6.10 / A4;
/// A2 5.3). Defaults are declared both here as POCO defaults and as EF default values in the
/// configuration.
/// </para>
/// <para>
/// It derives from <see cref="VersionedEntity"/>: settings are a mutable aggregate that an Owner
/// edits and that must be protected from stale overwrites. "Single row" is enforced by the
/// settings use case / endpoint added by a later task (there is one well-known settings record);
/// this type models the stored shape and conventions only.
/// </para>
/// </remarks>
public class BusinessSettings : VersionedEntity
{
    /// <summary>The IANA business timezone. Defaults to <c>America/Los_Angeles</c>.</summary>
    public string Timezone { get; set; } = "America/Los_Angeles";

    /// <summary>The ISO 4217 currency code for monetary values. Defaults to <c>USD</c>.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>The default labor rate (decimal(19,6)) applied where a specific rate is absent.</summary>
    public decimal DefaultLaborRate { get; set; }

    /// <summary>The default overhead rate (decimal(19,6)) applied where a specific rate is absent.</summary>
    public decimal DefaultOverheadRate { get; set; }

    /// <summary>The default unit of measure for dimensions (for example, "in" or "cm").</summary>
    public string DefaultDimensionUnit { get; set; } = "in";

    /// <summary>
    /// The image upload size-limit warning threshold in bytes. Defaults to 20 MB (requirement 6.10).
    /// </summary>
    public long ImageSizeLimitBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>
    /// The video upload size-limit warning threshold in bytes. Defaults to 200 MB (requirement 6.10).
    /// </summary>
    public long VideoSizeLimitBytes { get; set; } = 200L * 1024 * 1024;
}
