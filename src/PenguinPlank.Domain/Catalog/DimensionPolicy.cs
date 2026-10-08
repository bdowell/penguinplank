using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// Pure domain policy validating the physical dimensions recorded for a
/// <see cref="Product"/>, <see cref="ProductVariant"/>, or <see cref="ProductPiece"/>
/// (requirements R01 / 1.3, 1.4).
/// </summary>
/// <remarks>
/// <para>
/// Every supplied dimension must be a <b>non-negative</b> decimal with <b>no more
/// than four fractional digits of meaning</b>, and whenever <em>any</em> dimension
/// is supplied an <b>explicit, non-empty unit</b> must accompany it (requirement
/// 1.3). A round product may supply <see cref="ProductVariant.Diameter"/> in place
/// of <see cref="ProductVariant.Length"/>/<see cref="ProductVariant.Width"/>; the
/// same non-negative, four-decimal, explicit-unit rules apply to the diameter
/// (requirement 1.4). The length/width
/// and diameter forms are not mutually exclusive at this layer — the policy accepts
/// any combination of supplied values so long as each one is individually valid —
/// so a caller that offers either shape satisfies the rule.
/// </para>
/// <para>
/// <b>Precision expectation.</b> Dimensions persist as <c>decimal(12,4)</c>, so the
/// storage column enforces scale 4. This policy independently rejects any value
/// carrying more than four fractional digits of meaning rather than relying on the
/// database to silently round, so a request that would lose precision on write is
/// refused up front. A value whose extra fractional digits are only trailing zeros
/// (for example <c>1.50000</c>, which equals <c>1.5000</c> at scale 4) is accepted
/// because it loses no meaning when stored.
/// </para>
/// <para>
/// The policy is a <see langword="static"/> pure function (coding-standards §1, §3):
/// it takes explicit inputs, reads no clock, database, configuration, or ambient
/// state, mutates nothing, and returns a typed <see cref="Result"/>. A rejection
/// therefore leaves any caller-held record unchanged — the policy never touches it.
/// </para>
/// </remarks>
public static class DimensionPolicy
{
    /// <summary>The maximum number of fractional digits a dimension may carry (<c>decimal(12,4)</c>).</summary>
    public const int MaximumDecimalPlaces = 4;

    /// <summary>
    /// Validates the dimensions of a catalog record expressed as rectilinear
    /// measurements (<paramref name="length"/>/<paramref name="width"/>/
    /// <paramref name="thickness"/>) and/or a round <paramref name="diameter"/>,
    /// all sharing a single <paramref name="unit"/>.
    /// </summary>
    /// <param name="length">The length, or <see langword="null"/> when not supplied.</param>
    /// <param name="width">The width, or <see langword="null"/> when not supplied.</param>
    /// <param name="thickness">The thickness/height, or <see langword="null"/> when not supplied.</param>
    /// <param name="diameter">
    /// The diameter for a round product supplied in place of
    /// <paramref name="length"/>/<paramref name="width"/> (requirement 1.4), or
    /// <see langword="null"/> when not supplied.
    /// </param>
    /// <param name="unit">
    /// The explicit unit shared by every supplied dimension (for example "in" or
    /// "cm"). Required and non-empty whenever any dimension is supplied; ignored
    /// when no dimension is supplied.
    /// </param>
    /// <returns>
    /// <see cref="Result.Success()"/> when every supplied dimension is a
    /// non-negative decimal with at most four fractional digits of meaning and an
    /// explicit unit is present; otherwise a failure carrying
    /// <see cref="ErrorCode.InvalidDimension"/>.
    /// </returns>
    public static Result Validate(
        decimal? length,
        decimal? width,
        decimal? thickness,
        decimal? diameter,
        string? unit)
    {
        bool hasAnyDimension =
            length.HasValue
            || width.HasValue
            || thickness.HasValue
            || diameter.HasValue;

        if (!hasAnyDimension)
        {
            // No dimensions recorded is a legitimate absence: nothing to validate,
            // and no unit is required (requirement 1.3 governs supplied dimensions).
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            return Result.Failure(
                ErrorCode.InvalidDimension,
                "A dimension requires an explicit, non-empty unit.");
        }

        Result lengthResult = ValidateComponent(length, nameof(length));
        if (lengthResult.IsFailure)
        {
            return lengthResult;
        }

        Result widthResult = ValidateComponent(width, nameof(width));
        if (widthResult.IsFailure)
        {
            return widthResult;
        }

        Result thicknessResult = ValidateComponent(thickness, nameof(thickness));
        if (thicknessResult.IsFailure)
        {
            return thicknessResult;
        }

        return ValidateComponent(diameter, nameof(diameter));
    }

    private static Result ValidateComponent(decimal? value, string name)
    {
        if (!value.HasValue)
        {
            return Result.Success();
        }

        decimal measurement = value.Value;

        if (measurement < 0m)
        {
            return Result.Failure(
                ErrorCode.InvalidDimension,
                $"The {name} dimension must be a non-negative decimal.");
        }

        if (FractionalDigitCount(measurement) > MaximumDecimalPlaces)
        {
            return Result.Failure(
                ErrorCode.InvalidDimension,
                $"The {name} dimension must have at most {MaximumDecimalPlaces} decimal places.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Counts the fractional digits of meaning in <paramref name="value"/>, ignoring
    /// trailing zeros. Normalizing through <c>value / 1.000...m</c> strips trailing
    /// zeros so that, for example, <c>1.50000</c> reports two fractional digits
    /// rather than five — it loses no meaning when stored at scale 4.
    /// </summary>
    private static int FractionalDigitCount(decimal value)
    {
        // The scale is encoded in bits 16-23 of the fourth int of the decimal's
        // bit layout. Dividing by 1.000000000000000000000000000000000m forces the
        // runtime to drop trailing-zero scale, leaving only significant fractional
        // digits.
        decimal normalized = value / 1.000000000000000000000000000000000m;
        int scale = (decimal.GetBits(normalized)[3] >> 16) & 0xFF;
        return scale;
    }
}
