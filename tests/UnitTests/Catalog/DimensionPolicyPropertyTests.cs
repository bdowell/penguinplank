using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog;

/// <summary>
/// Property 2 (requirement R01 / 1.3, 1.4): <em>Dimension validation.</em>
/// <see cref="DimensionPolicy.Validate"/> is a pure, directly testable domain
/// policy (coding-standards Â§1, Â§7): it takes explicit nullable decimals plus a
/// unit string, reads no clock, database, configuration, or ambient state,
/// mutates nothing, and returns a typed <see cref="Result"/>. These properties
/// assert the policy's contract across the whole input space:
/// <list type="number">
///   <item><b>All-null is accepted.</b> When no dimension is supplied the policy
///   succeeds regardless of the unit â€” a legitimate absence requires no unit
///   (requirement 1.3).</item>
///   <item><b>Supplied dimension without a unit is rejected.</b> When any
///   dimension is supplied and the unit is <see langword="null"/>, empty, or
///   whitespace the policy fails with <see cref="ErrorCode.InvalidDimension"/>
///   (requirement 1.3).</item>
///   <item><b>Negative dimension is rejected.</b> With a valid unit, any supplied
///   negative dimension yields <see cref="ErrorCode.InvalidDimension"/>.</item>
///   <item><b>Over-precise dimension is rejected; trailing zeros are not.</b> With
///   a valid unit and non-negative values, a dimension carrying more than four
///   fractional digits of <em>meaning</em> is rejected, whereas a value padded
///   only with trailing zeros (for example <c>2.50000</c>) is not rejected for
///   precision â€” it loses nothing when stored at scale 4.</item>
///   <item><b>Well-formed dimensions are accepted.</b> Non-negative values with at
///   most four fractional digits and a non-empty unit succeed.</item>
///   <item><b>Round product via diameter.</b> Supplying only a valid diameter
///   (non-negative, â‰¤4 dp, with a unit) succeeds in place of length/width
///   (requirement 1.4).</item>
/// </list>
/// </summary>
/// <remarks>
/// This is a pure-value property test exercising the policy with ordinary values;
/// it needs no database, no <c>HttpContext</c>, and no application startup
/// (coding-standards Â§1, Â§7). Generators craft in-range and out-of-range decimals
/// and controlled fractional-digit counts, and unit strings spanning empty,
/// whitespace, and non-empty. The precision generators align with the policy's
/// trailing-zero semantics (<see cref="DimensionPolicy"/>'s normalization strips
/// trailing zeros before counting), so a "too precise" value always carries a
/// non-zero digit beyond the fourth fractional place. CsCheck (pinned in
/// <c>UnitTests.csproj</c>) runs each property at <see cref="Iterations"/> samples.
/// </remarks>
public class DimensionPolicyPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every
    /// correctness property; this sits well above that floor.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Generates a non-negative dimension with at most four fractional digits of
    /// meaning: an integer scaled by up to 10,000 then divided back, so the result
    /// always stores losslessly at scale 4.
    /// </summary>
    private static readonly Gen<decimal> s_genValidMeasurement =
        Gen.Int[0, 10_000_000].Select(scaled => scaled / 10_000m);

    /// <summary>
    /// Generates a non-negative dimension with the given number of fractional
    /// digits of meaning (its final fractional digit is forced non-zero so the
    /// scale is not reduced by trailing-zero normalization).
    /// </summary>
    private static Gen<decimal> GenMeasurementWithScale(int scale)
    {
        decimal divisor = Pow10(scale);
        // Build an integer whose last decimal digit is 1-9 (never 0) so dividing by
        // 10^scale yields a value with exactly `scale` significant fractional digits.
        return Gen.Select(Gen.Int[0, 1_000_000], Gen.Int[1, 9])
            .Select(tuple => (tuple.Item1 * 10 + tuple.Item2) / divisor);
    }

    /// <summary>Generates a strictly negative dimension.</summary>
    private static readonly Gen<decimal> s_genNegativeMeasurement =
        Gen.Int[1, 10_000_000].Select(scaled => -(scaled / 10_000m));

    /// <summary>Generates a non-empty unit string.</summary>
    private static readonly Gen<string> s_genValidUnit =
        Gen.OneOfConst("in", "cm", "mm", "ft", "m");

    /// <summary>Generates a null/empty/whitespace unit (an invalid unit).</summary>
    private static readonly Gen<string?> s_genBlankUnit =
        Gen.OneOfConst<string?>(null, string.Empty, " ", "   ", "\t", "\n", " \t ");

    /// <summary>Generates any unit â€” blank or non-empty â€” mixed together.</summary>
    private static readonly Gen<string?> s_genAnyUnit =
        Gen.Frequency(
            (1, s_genBlankUnit),
            (1, s_genValidUnit.Select(unit => (string?)unit)));

    /// <summary>
    /// Generates an optional valid measurement: <see langword="null"/> (not
    /// supplied) mixed with a non-negative â‰¤4 dp value.
    /// </summary>
    private static readonly Gen<decimal?> s_genOptionalValidMeasurement =
        Gen.Frequency(
            (1, Gen.Const((decimal?)null)),
            (2, s_genValidMeasurement.Select(value => (decimal?)value)));

    [Fact]
    public void Validate_NoDimensionSupplied_SucceedsRegardlessOfUnit()
    {
        s_genAnyUnit.Sample(
            unit =>
            {
                Result result = DimensionPolicy.Validate(
                    length: null,
                    width: null,
                    thickness: null,
                    diameter: null,
                    unit: unit);

                Assert.True(result.IsSuccess);
            },
            iter: Iterations);
    }

    [Fact]
    public void Validate_DimensionSuppliedWithBlankUnit_RejectsInvalidDimension()
    {
        // At least one dimension is always supplied so the blank-unit rule applies.
        Gen.Select(
                s_genValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genBlankUnit)
            .Sample(
                tuple =>
                {
                    Result result = DimensionPolicy.Validate(
                        length: tuple.Item1,
                        width: tuple.Item2,
                        thickness: tuple.Item3,
                        diameter: tuple.Item4,
                        unit: tuple.Item5);

                    AssertRejectedAsInvalidDimension(result);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_NegativeDimensionWithValidUnit_RejectsInvalidDimension()
    {
        // The length is forced negative; the remaining dimensions are valid or
        // absent, so the only reason to reject is the negative value.
        Gen.Select(
                s_genNegativeMeasurement,
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genValidUnit)
            .Sample(
                tuple =>
                {
                    Result result = DimensionPolicy.Validate(
                        length: tuple.Item1,
                        width: tuple.Item2,
                        thickness: tuple.Item3,
                        diameter: tuple.Item4,
                        unit: tuple.Item5);

                    AssertRejectedAsInvalidDimension(result);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_OverPreciseDimensionWithValidUnit_RejectsInvalidDimension()
    {
        // The length carries 5..10 fractional digits of meaning (final digit never
        // a trailing zero), so it must be rejected for precision.
        Gen.Select(
                Gen.Int[DimensionPolicy.MaximumDecimalPlaces + 1, 10]
                    .SelectMany(GenMeasurementWithScale),
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genValidUnit)
            .Sample(
                tuple =>
                {
                    Result result = DimensionPolicy.Validate(
                        length: tuple.Item1,
                        width: tuple.Item2,
                        thickness: tuple.Item3,
                        diameter: tuple.Item4,
                        unit: tuple.Item5);

                    AssertRejectedAsInvalidDimension(result);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_TrailingZeroPaddedDimensionWithValidUnit_NotRejectedForPrecision()
    {
        // A value whose fourth-decimal meaning is padded with extra trailing zeros
        // (for example 2.5000 or 2.50000) stores losslessly at scale 4 and must be
        // accepted â€” the policy counts digits of meaning, not raw scale.
        Gen.Select(
                s_genValidMeasurement,
                Gen.Int[0, 6],
                s_genValidUnit)
            .Sample(
                tuple =>
                {
                    decimal padded = PadTrailingZeros(tuple.Item1, tuple.Item2);

                    Result result = DimensionPolicy.Validate(
                        length: padded,
                        width: null,
                        thickness: null,
                        diameter: null,
                        unit: tuple.Item3);

                    Assert.True(result.IsSuccess);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_WellFormedDimensionsWithValidUnit_Succeeds()
    {
        Gen.Select(
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genOptionalValidMeasurement,
                s_genValidUnit)
            .Where(tuple =>
                tuple.Item1.HasValue
                || tuple.Item2.HasValue
                || tuple.Item3.HasValue
                || tuple.Item4.HasValue)
            .Sample(
                tuple =>
                {
                    Result result = DimensionPolicy.Validate(
                        length: tuple.Item1,
                        width: tuple.Item2,
                        thickness: tuple.Item3,
                        diameter: tuple.Item4,
                        unit: tuple.Item5);

                    Assert.True(result.IsSuccess);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_RoundProductDiameterOnlyWithValidUnit_Succeeds()
    {
        // Requirement 1.4: a round product may supply diameter in place of
        // length/width. Only the diameter is populated here.
        Gen.Select(s_genValidMeasurement, s_genValidUnit)
            .Sample(
                tuple =>
                {
                    Result result = DimensionPolicy.Validate(
                        length: null,
                        width: null,
                        thickness: null,
                        diameter: tuple.Item1,
                        unit: tuple.Item2);

                    Assert.True(result.IsSuccess);
                },
                iter: Iterations);
    }

    /// <summary>
    /// Asserts a rejection carries the stable <see cref="ErrorCode.InvalidDimension"/>
    /// code and leaves no success value exposed.
    /// </summary>
    private static void AssertRejectedAsInvalidDimension(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.InvalidDimension, result.Error.Code);
    }

    /// <summary>Returns 10 raised to <paramref name="exponent"/> as a decimal.</summary>
    private static decimal Pow10(int exponent)
    {
        decimal result = 1m;
        for (int i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }

    /// <summary>
    /// Returns <paramref name="value"/> re-expressed with <paramref name="extraZeros"/>
    /// additional trailing-zero fractional digits beyond scale 4, preserving its
    /// numeric meaning. Multiplying then dividing by a power of ten inflates the
    /// decimal's raw scale without changing its value.
    /// </summary>
    private static decimal PadTrailingZeros(decimal value, int extraZeros)
    {
        decimal factor = Pow10(extraZeros);
        return value * factor / factor;
    }
}
