using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog;

/// <summary>
/// Property 3 (requirements R01 Â§1.5, R10 Â§2.2): <em>Wood composition proportion validation.</em>
/// <see cref="WoodCompositionPolicy"/> is a pure static policy over an explicit collection of
/// nullable proportions, so it is directly testable with ordinary values and needs no entities,
/// database, or application startup (coding-standards Â§1, Â§7). These properties assert the
/// accept-iff-all-in-range rule across the whole input space:
/// <list type="number">
///   <item><b>Accept when all in range.</b> A collection whose every supplied (non-null)
///   proportion lies within <see cref="WoodCompositionPolicy.MinimumProportion"/> to
///   <see cref="WoodCompositionPolicy.MaximumProportion"/> inclusive â€” mixed freely with
///   <see langword="null"/> entries â€” is accepted with <see cref="Result.Success()"/>.</item>
///   <item><b>Reject when any out of range.</b> A collection containing at least one supplied
///   proportion below 0 or above 100 is rejected with a failure carrying
///   <see cref="ErrorCode.InvalidProportion"/>.</item>
///   <item><b>Null / empty is accepted.</b> A <see langword="null"/> or empty collection has no
///   proportion to constrain and is accepted.</item>
///   <item><b>Nulls never fail.</b> A collection of only <see langword="null"/> entries is always
///   accepted; null is an unquantified presence, never a range violation.</item>
///   <item><b>Single-value predicate.</b>
///   <see cref="WoodCompositionPolicy.IsSuppliedProportionInRange(decimal?)"/> returns
///   <see langword="true"/> exactly when the value is <see langword="null"/> or in [0,100].</item>
/// </list>
/// </summary>
/// <remarks>
/// Generators produce in-range values ([0,100]), out-of-range values (strictly below 0 or above
/// 100), and <see langword="null"/> entries, then assemble mixed collections so both the accept and
/// reject branches are exercised. CsCheck (pinned in <c>UnitTests.csproj</c>) runs each property at
/// <see cref="Iterations"/> samples, well above the design's â‰¥100 floor.
/// </remarks>
public class WoodCompositionPolicyPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Generates a supplied proportion within the inclusive [0,100] range, including the exact
    /// boundaries, as a non-null decimal. The value is built by scaling an integer so it stores
    /// losslessly at four fractional digits and always lands in
    /// <see cref="WoodCompositionPolicy.MinimumProportion"/>..<see cref="WoodCompositionPolicy.MaximumProportion"/>
    /// (0 .. 1,000,000 / 10,000 == 0 .. 100).
    /// </summary>
    private static readonly Gen<decimal?> s_genInRange =
        Gen.Int[0, 1_000_000].Select(scaled => (decimal?)(scaled / 10_000m));

    /// <summary>
    /// Generates a supplied proportion strictly outside the [0,100] range: either below the
    /// minimum (a strictly negative value) or above the maximum (strictly greater than 100). Each
    /// branch scales an integer the same lossless way, so no value can accidentally fall in range.
    /// </summary>
    private static readonly Gen<decimal?> s_genOutOfRange =
        Gen.Frequency(
            // (-1,000 .. -0.0001]: strictly below the minimum of 0.
            (1, Gen.Int[1, 10_000_000].Select(scaled => (decimal?)(-(scaled / 10_000m)))),
            // (100 .. 2,000]: strictly above the maximum of 100 (1,000,001 / 10,000 == 100.0001).
            (1, Gen.Int[1_000_001, 20_000_000].Select(scaled => (decimal?)(scaled / 10_000m))));

    /// <summary>A null entry: an unquantified association that is always acceptable.</summary>
    private static readonly Gen<decimal?> s_genNull =
        Gen.Const(0).Select(_ => (decimal?)null);

    /// <summary>
    /// Generates an entry that is acceptable on its own: either in range or null. Collections of
    /// only these entries must always be accepted.
    /// </summary>
    private static readonly Gen<decimal?> s_genAcceptableEntry =
        Gen.Frequency(
            (3, s_genInRange),
            (1, s_genNull));

    /// <summary>A collection whose every entry is acceptable (in range or null).</summary>
    private static readonly Gen<decimal?[]> s_genAllAcceptable =
        s_genAcceptableEntry.Array[0, 12];

    [Fact]
    public void Validate_AllSuppliedProportionsInRange_AcceptsComposition()
    {
        s_genAllAcceptable.Sample(
            proportions =>
            {
                Result result = WoodCompositionPolicy.Validate(proportions);

                Assert.True(result.IsSuccess);
            },
            iter: Iterations);
    }

    [Fact]
    public void Validate_AnySuppliedProportionOutOfRange_RejectsWithInvalidProportion()
    {
        // Build a collection of acceptable entries, inject at least one out-of-range value at an
        // arbitrary position, so the only reason to reject is the out-of-range entry.
        Gen<(decimal?[] Acceptable, decimal? Offending, int Index)> gen =
            s_genAllAcceptable
                .SelectMany(acceptable => s_genOutOfRange
                    .SelectMany(offending => Gen.Int[0, acceptable.Length]
                        .Select(index => (acceptable, offending, index))));

        gen.Sample(
            sample =>
            {
                List<decimal?> proportions = [.. sample.Acceptable];
                proportions.Insert(sample.Index, sample.Offending);

                Result result = WoodCompositionPolicy.Validate(proportions);

                Assert.True(result.IsFailure);
                Assert.Equal(ErrorCode.InvalidProportion, result.Error.Code);
            },
            iter: Iterations);
    }

    [Fact]
    public void Validate_NullOrEmptyCollection_AcceptsComposition()
    {
        // true => a null collection; false => an empty collection. Both are "no constraints".
        Gen.Bool
            .Select(useNull => useNull
                ? (IEnumerable<decimal?>?)null
                : Array.Empty<decimal?>())
            .Sample(
                proportions =>
                {
                    Result result = WoodCompositionPolicy.Validate(proportions);

                    Assert.True(result.IsSuccess);
                },
                iter: Iterations);
    }

    [Fact]
    public void Validate_OnlyNullEntries_NeverCauseFailure()
    {
        s_genNull.Array[0, 12].Sample(
            proportions =>
            {
                Result result = WoodCompositionPolicy.Validate(proportions);

                Assert.True(result.IsSuccess);
            },
            iter: Iterations);
    }

    [Fact]
    public void IsSuppliedProportionInRange_NullOrInRange_ReturnsTrueOtherwiseFalse()
    {
        Gen.Frequency(
                (2, s_genInRange),
                (2, s_genOutOfRange),
                (1, s_genNull))
            .Sample(
                proportion =>
                {
                    bool actual = WoodCompositionPolicy.IsSuppliedProportionInRange(proportion);

                    bool expected = proportion is not decimal value
                        || (value >= WoodCompositionPolicy.MinimumProportion
                            && value <= WoodCompositionPolicy.MaximumProportion);

                    Assert.Equal(expected, actual);
                },
                iter: Iterations);
    }
}
