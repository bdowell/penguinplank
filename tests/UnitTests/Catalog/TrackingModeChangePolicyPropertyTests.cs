using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog;

/// <summary>
/// Property 5 (task 7.10, requirement R01 Â§1.13): <em>Tracking-mode change guard.</em>
/// <see cref="TrackingModeChangePolicy.CanChange"/> is a pure static decision (coding-standards
/// Â§1, Â§7): it receives the current and requested <see cref="TrackingMode"/> values together with
/// whether stock history exists and whether a designed migration applies as <b>explicit inputs</b>
/// and returns a <see cref="Result"/> without any I/O. These properties assert the exact truth
/// table across the whole input space:
/// <list type="number">
///   <item><b>Success condition.</b> The decision succeeds <em>iff</em>
///   <c>current == requested</c> (a no-op), <c>!hasStockHistory</c>, or <c>migrationApplied</c>.</item>
///   <item><b>Sole failure case.</b> The only rejected input is a real change
///   (<c>current != requested</c>) against a variant with stock history and no designed migration;
///   that failure carries <see cref="ErrorCode.TrackingModeLocked"/> and a non-empty message.</item>
/// </list>
/// </summary>
/// <remarks>
/// The input space is tiny â€” the full cross product of (current, requested) in
/// {<see cref="TrackingMode.Serialized"/>, <see cref="TrackingMode.Quantity"/>} by
/// <c>hasStockHistory</c> by <c>migrationApplied</c> is only 16 combinations â€” so it is both
/// enumerated exhaustively as a theory and sampled randomly by CsCheck (pinned in
/// <c>UnitTests.csproj</c>) at <see cref="Iterations"/> samples (â‰¥100 per the design floor). The
/// test uses ordinary values and needs no database, no <c>HttpContext</c>, and no application
/// startup (coding-standards Â§1, Â§7).
/// </remarks>
public class TrackingModeChangePolicyPropertyTests
{
    /// <summary>
    /// Iterations per CsCheck property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor so the 16-combination input space is sampled
    /// many times over.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>Generates one of the two legal tracking modes.</summary>
    private static readonly Gen<TrackingMode> s_genTrackingMode =
        Gen.Int[0, 1].Select(index => (TrackingMode)index);

    /// <summary>Generates the full input tuple over the cross product plus random booleans.</summary>
    private static readonly Gen<(TrackingMode Current, TrackingMode Requested, bool HasStockHistory, bool MigrationApplied)> s_genInput =
        Gen.Select(s_genTrackingMode, s_genTrackingMode, Gen.Bool, Gen.Bool);

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void CanChange_EveryCombination_MatchesTruthTable(
        TrackingMode current,
        TrackingMode requested,
        bool hasStockHistory,
        bool migrationApplied)
    {
        Result result = TrackingModeChangePolicy.CanChange(current, requested, hasStockHistory, migrationApplied);

        AssertMatchesTruthTable(current, requested, hasStockHistory, migrationApplied, result);
    }

    [Fact]
    public void CanChange_AnyInput_SucceedsExactlyWhenNoOpOrNoHistoryOrMigrated()
    {
        s_genInput.Sample(
            input =>
            {
                Result result = TrackingModeChangePolicy.CanChange(
                    input.Current,
                    input.Requested,
                    input.HasStockHistory,
                    input.MigrationApplied);

                AssertMatchesTruthTable(
                    input.Current,
                    input.Requested,
                    input.HasStockHistory,
                    input.MigrationApplied,
                    result);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanChange_ChangeWithStockHistoryAndNoMigration_IsTheOnlyFailureAndIsLocked()
    {
        s_genInput.Sample(
            input =>
            {
                Result result = TrackingModeChangePolicy.CanChange(
                    input.Current,
                    input.Requested,
                    input.HasStockHistory,
                    input.MigrationApplied);

                bool isLockedCase =
                    input.Current != input.Requested
                    && input.HasStockHistory
                    && !input.MigrationApplied;

                if (isLockedCase)
                {
                    Assert.True(result.IsFailure);
                    Assert.Equal(ErrorCode.TrackingModeLocked, result.Error.Code);
                    Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
                }
                else
                {
                    Assert.True(result.IsSuccess);
                }
            },
            iter: Iterations);
    }

    /// <summary>The full 16-combination cross product used by the exhaustive theory.</summary>
    public static TheoryData<TrackingMode, TrackingMode, bool, bool> AllCombinations()
    {
        TheoryData<TrackingMode, TrackingMode, bool, bool> data = new();

        TrackingMode[] modes = { TrackingMode.Serialized, TrackingMode.Quantity };
        bool[] flags = { false, true };

        foreach (TrackingMode current in modes)
        {
            foreach (TrackingMode requested in modes)
            {
                foreach (bool hasStockHistory in flags)
                {
                    foreach (bool migrationApplied in flags)
                    {
                        data.Add(current, requested, hasStockHistory, migrationApplied);
                    }
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Asserts the policy outcome against the specification truth table: success iff the change is
    /// a no-op, no stock history exists, or a designed migration applies; otherwise a
    /// <see cref="ErrorCode.TrackingModeLocked"/> failure.
    /// </summary>
    private static void AssertMatchesTruthTable(
        TrackingMode current,
        TrackingMode requested,
        bool hasStockHistory,
        bool migrationApplied,
        Result result)
    {
        bool expectedSuccess =
            current == requested
            || !hasStockHistory
            || migrationApplied;

        if (expectedSuccess)
        {
            Assert.True(result.IsSuccess);
        }
        else
        {
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCode.TrackingModeLocked, result.Error.Code);
            Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
        }
    }
}
