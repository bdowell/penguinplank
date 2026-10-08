using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog;

/// <summary>
/// Property 6 (requirement R01 / 1.8): <em>Archived master data rejects new transactions.</em>
/// <see cref="ArchivedMasterDataPolicy"/> is the pure decision that admits a new transaction only
/// when every catalog master-data record it targets is active. Archiving is a soft retirement, so
/// a new sale, stock movement, production record, or linkage against an archived
/// (<c>ActiveFlag == false</c>) record must be refused with <see cref="ErrorCode.ArchivedRecord"/>,
/// while existing history stays readable. These properties assert that admission rule holds across
/// many inputs:
/// <list type="number">
///   <item>The single-flag overload admits an active target and refuses an archived one with
///   <see cref="ErrorCode.ArchivedRecord"/>.</item>
///   <item>The collection overload is a success <em>iff</em> every referenced flag is active; a
///   single archived entry refuses the whole transaction with
///   <see cref="ErrorCode.ArchivedRecord"/>.</item>
///   <item>A <see langword="null"/> or empty collection references no master data and is
///   admitted.</item>
/// </list>
/// </summary>
/// <remarks>
/// This is a pure-policy property test: it exercises <see cref="ArchivedMasterDataPolicy"/> with
/// ordinary boolean values and needs no database, no <c>HttpContext</c>, and no application
/// startup (coding-standards Â§1, Â§7). Generators cover empty lists, all-active lists, and lists
/// guaranteed to carry at least one archived entry. CsCheck (pinned in <c>UnitTests.csproj</c>)
/// runs each property at <see cref="Iterations"/> samples, above the design's â‰¥100 floor.
/// </remarks>
public class ArchivedMasterDataPolicyPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Generates an arbitrary list of active flags: 0 to 16 entries with arbitrary boolean values.
    /// The range includes the empty list (a transaction referencing no master data), all-active
    /// lists, and lists carrying one or more archived entries.
    /// </summary>
    private static readonly Gen<bool[]> s_genFlags =
        Gen.Bool.Array[0, 16];

    /// <summary>
    /// Generates a non-empty list in which every entry is active (<see langword="true"/>),
    /// modelling a transaction whose every referenced record admits new activity.
    /// </summary>
    private static readonly Gen<bool[]> s_genAllActive =
        Gen.Int[1, 16].Select(length => CreateAllActive(length));

    /// <summary>
    /// Generates a list guaranteed to carry at least one archived entry: an arbitrary list of flags
    /// with a <see langword="false"/> spliced in at a random position, modelling a transaction that
    /// must be refused.
    /// </summary>
    private static readonly Gen<bool[]> s_genWithArchived =
        Gen.Select(s_genFlags, Gen.Int[0, 16])
            .Select(pair => InsertArchived(pair.Item1, pair.Item2));

    [Fact]
    public void CanTransact_SingleActiveTarget_AdmitsTransaction()
    {
        Result result = ArchivedMasterDataPolicy.CanTransact(isTargetActive: true);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanTransact_SingleArchivedTarget_RejectsWithArchivedRecord()
    {
        Result result = ArchivedMasterDataPolicy.CanTransact(isTargetActive: false);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
    }

    [Fact]
    public void CanTransact_AnyFlagCollection_SucceedsIffEveryTargetIsActive()
    {
        s_genFlags.Sample(
            flags =>
            {
                Result result = ArchivedMasterDataPolicy.CanTransact(flags);

                bool everyTargetActive = Array.TrueForAll(flags, isActive => isActive);
                Assert.Equal(everyTargetActive, result.IsSuccess);

                // A rejection always carries the stable archived-record code; a transaction is
                // only ever refused because a referenced record is archived.
                if (result.IsFailure)
                {
                    Assert.False(everyTargetActive);
                    Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
                }
            },
            iter: Iterations);
    }

    [Fact]
    public void CanTransact_AllTargetsActive_AdmitsTransaction()
    {
        s_genAllActive.Sample(
            flags =>
            {
                Result result = ArchivedMasterDataPolicy.CanTransact(flags);

                Assert.True(result.IsSuccess);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanTransact_AtLeastOneArchivedTarget_RejectsWithArchivedRecord()
    {
        s_genWithArchived.Sample(
            flags =>
            {
                // The generator guarantees at least one archived entry.
                Assert.Contains(false, flags);

                Result result = ArchivedMasterDataPolicy.CanTransact(flags);

                Assert.True(result.IsFailure);
                Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanTransact_NullCollection_AdmitsTransaction()
    {
        Result result = ArchivedMasterDataPolicy.CanTransact((IEnumerable<bool>?)null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanTransact_EmptyCollection_AdmitsTransaction()
    {
        Result result = ArchivedMasterDataPolicy.CanTransact(Array.Empty<bool>());

        Assert.True(result.IsSuccess);
    }

    private static bool[] CreateAllActive(int length)
    {
        bool[] flags = new bool[length];
        Array.Fill(flags, true);
        return flags;
    }

    private static bool[] InsertArchived(bool[] flags, int position)
    {
        int clampedPosition = position > flags.Length ? flags.Length : position;
        bool[] withArchived = new bool[flags.Length + 1];

        Array.Copy(flags, 0, withArchived, 0, clampedPosition);
        withArchived[clampedPosition] = false;
        Array.Copy(flags, clampedPosition, withArchived, clampedPosition + 1, flags.Length - clampedPosition);

        return withArchived;
    }
}
