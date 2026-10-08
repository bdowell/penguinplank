using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog;

/// <summary>
/// Properties 7 and 8 (requirements R10 / 2.3, 2.4, 2.5): care-profile versioning is
/// <em>append-only</em> and a produced piece <em>preserves</em> the historical version in effect at
/// its production instant. The pure policy under test â€” <see cref="CareVersionResolver"/> â€” computes
/// the next version number to append and resolves the version in effect at a production instant from
/// explicit inputs, touching no database and starting no application, so these properties exercise it
/// directly across a wide input space (coding-standards Â§1, Â§7).
/// <list type="number">
///   <item><b>Property 7 â€” append-only next version (2.3, 2.5).</b> The next version number is one
///   greater than the maximum existing version number and <c>1</c> for a null/empty profile; the
///   <see cref="int"/> overload maps a current maximum below <see cref="CareVersionResolver.FirstVersionNumber"/>
///   to <c>1</c> and otherwise to <c>currentMax + 1</c>. The computed next number is strictly greater
///   than every existing version number (monotonic/append-only) and the call never mutates the input
///   collection.</item>
///   <item><b>Property 8 â€” produced-piece version preservation (2.4).</b> Resolving a production
///   instant returns the latest version created at or before it, and appending a <em>newer</em>
///   version (created after that instant) does not change the resolved version for the earlier
///   instant. An instant before the earliest version â€” and an empty/null collection â€” fails with
///   <see cref="ErrorCode.Validation"/>.</item>
/// </list>
/// </summary>
/// <remarks>
/// Version sets are generated with strictly increasing creation instants and version numbers so each
/// instant has an unambiguous "latest at or before" answer; production instants are drawn relative to
/// those instants so both the hit and the before-earliest cases are exercised. CsCheck (pinned in
/// <c>UnitTests.csproj</c>) runs each property at <see cref="Iterations"/> samples (â‰¥100 as the design
/// mandates).
/// </remarks>
public class CareVersionResolverPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness property;
    /// this matches the suite convention of sampling well above that floor.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>The base instant from which generated version creation instants ascend.</summary>
    private static readonly DateTimeOffset s_baseInstant =
        new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Generates a non-empty ordered set of <see cref="CareProfileVersion"/> with strictly
    /// increasing <see cref="CareProfileVersion.VersionNumber"/> (starting at 1) and strictly
    /// increasing <see cref="Entity.CreatedAtUtc"/>, so the "latest at or before" answer is always
    /// unambiguous. The gaps between successive instants are at least one minute, leaving room to
    /// pick an instant strictly between two versions.
    /// </summary>
    private static readonly Gen<CareProfileVersion[]> s_genVersions =
        Gen.Int[1, 8].SelectMany(count =>
            Gen.Int[1, 60].Array[count].Select(gaps =>
            {
                Guid profileId = Guid.NewGuid();
                var versions = new CareProfileVersion[count];
                DateTimeOffset createdAt = s_baseInstant;

                for (int index = 0; index < count; index++)
                {
                    createdAt = createdAt.AddMinutes(gaps[index]);
                    versions[index] = new CareProfileVersion
                    {
                        Id = Guid.NewGuid(),
                        CareProfileId = profileId,
                        VersionNumber = index + 1,
                        Guidance = "guidance-" + (index + 1),
                        CreatedAtUtc = createdAt,
                        UpdatedAtUtc = createdAt,
                    };
                }

                return versions;
            }));

    // ----- Property 7: append-only next version (R10 / 2.3, 2.5) -----

    [Fact]
    public void NextVersionNumber_IntOverload_ReturnsFirstBelowFloorOtherwiseIncrement()
    {
        Gen.Int[-5, 10000].Sample(
            currentMax =>
            {
                int next = CareVersionResolver.NextVersionNumber(currentMax);

                int expected = currentMax < CareVersionResolver.FirstVersionNumber
                    ? CareVersionResolver.FirstVersionNumber
                    : currentMax + 1;

                Assert.Equal(expected, next);
            },
            iter: Iterations);
    }

    [Fact]
    public void NextVersionNumber_NullOrEmptyVersions_ReturnsFirstVersionNumber()
    {
        Gen.Bool.Sample(
            useNull =>
            {
                CareProfileVersion[]? existing = useNull ? null : Array.Empty<CareProfileVersion>();

                int next = CareVersionResolver.NextVersionNumber(existing);

                Assert.Equal(CareVersionResolver.FirstVersionNumber, next);
            },
            iter: Iterations);
    }

    [Fact]
    public void NextVersionNumber_ExistingVersions_ReturnsMaxPlusOneStrictlyGreaterThanAll()
    {
        s_genVersions.Sample(
            versions =>
            {
                int next = CareVersionResolver.NextVersionNumber(versions);

                int maxExisting = versions.Max(version => version.VersionNumber);

                Assert.Equal(maxExisting + 1, next);

                // Append-only / monotonic: the next number is strictly greater than every existing
                // version number, so an append can never collide with or precede an existing one.
                Assert.All(versions, version => Assert.True(next > version.VersionNumber));
            },
            iter: Iterations);
    }

    [Fact]
    public void NextVersionNumber_ExistingVersions_DoesNotMutateInputCollection()
    {
        s_genVersions.Sample(
            versions =>
            {
                // Snapshot identity, order, and the fields the resolver reads before the call.
                CareProfileVersion[] before = versions.ToArray();
                int[] numbersBefore = versions.Select(version => version.VersionNumber).ToArray();
                DateTimeOffset[] instantsBefore =
                    versions.Select(version => version.CreatedAtUtc).ToArray();

                _ = CareVersionResolver.NextVersionNumber(versions);

                Assert.Equal(before.Length, versions.Length);
                for (int index = 0; index < before.Length; index++)
                {
                    // Same instances, same order (reference equality).
                    Assert.Same(before[index], versions[index]);
                    Assert.Equal(numbersBefore[index], versions[index].VersionNumber);
                    Assert.Equal(instantsBefore[index], versions[index].CreatedAtUtc);
                }
            },
            iter: Iterations);
    }

    // ----- Property 8: produced-piece version preservation (R10 / 2.4) -----

    [Fact]
    public void ResolveForProduction_InstantAtOrAfterAVersion_ReturnsLatestVersionInEffect()
    {
        s_genVersions
            .SelectMany(versions =>
                Gen.Int[0, versions.Length - 1].Select(pick => (Versions: versions, Pick: pick)))
            .Sample(
                sample =>
                {
                    CareProfileVersion target = sample.Versions[sample.Pick];

                    // An instant between the picked version and the next one (or just after the last
                    // version) resolves to the picked version.
                    DateTimeOffset producedAtUtc = target.CreatedAtUtc.AddSeconds(1);

                    Result<CareProfileVersion> result =
                        CareVersionResolver.ResolveForProduction(sample.Versions, producedAtUtc);

                    CareProfileVersion expected = sample.Versions
                        .Where(version => version.CreatedAtUtc <= producedAtUtc)
                        .OrderByDescending(version => version.CreatedAtUtc)
                        .ThenByDescending(version => version.VersionNumber)
                        .First();

                    Assert.True(result.IsSuccess);
                    Assert.Same(expected, result.Value);
                },
                iter: Iterations);
    }

    [Fact]
    public void ResolveForProduction_AppendingNewerVersion_PreservesResolvedHistoricalVersion()
    {
        s_genVersions
            .SelectMany(versions =>
                Gen.Int[0, versions.Length - 1].Select(pick => (Versions: versions, Pick: pick)))
            .Sample(
                sample =>
                {
                    CareProfileVersion target = sample.Versions[sample.Pick];
                    DateTimeOffset producedAtUtc = target.CreatedAtUtc.AddSeconds(1);

                    Result<CareProfileVersion> before =
                        CareVersionResolver.ResolveForProduction(sample.Versions, producedAtUtc);

                    // Append a NEWER version created strictly after the production instant. By
                    // requirement 2.4 this later edit must not change the version resolved for the
                    // earlier production instant.
                    DateTimeOffset laterInstant = sample.Versions.Max(v => v.CreatedAtUtc).AddDays(1);
                    var newerVersion = new CareProfileVersion
                    {
                        Id = Guid.NewGuid(),
                        CareProfileId = target.CareProfileId,
                        VersionNumber = CareVersionResolver.NextVersionNumber(sample.Versions),
                        Guidance = "newer-guidance",
                        CreatedAtUtc = laterInstant,
                        UpdatedAtUtc = laterInstant,
                    };

                    CareProfileVersion[] withNewer =
                        sample.Versions.Append(newerVersion).ToArray();

                    Result<CareProfileVersion> after =
                        CareVersionResolver.ResolveForProduction(withNewer, producedAtUtc);

                    Assert.True(before.IsSuccess);
                    Assert.True(after.IsSuccess);
                    Assert.Same(before.Value, after.Value);
                },
                iter: Iterations);
    }

    [Fact]
    public void ResolveForProduction_InstantBeforeEarliestVersion_FailsWithValidation()
    {
        s_genVersions.Sample(
            versions =>
            {
                DateTimeOffset earliest = versions.Min(version => version.CreatedAtUtc);
                DateTimeOffset producedAtUtc = earliest.AddTicks(-1);

                Result<CareProfileVersion> result =
                    CareVersionResolver.ResolveForProduction(versions, producedAtUtc);

                Assert.True(result.IsFailure);
                Assert.Equal(ErrorCode.Validation, result.Error.Code);
            },
            iter: Iterations);
    }

    [Fact]
    public void ResolveForProduction_NullOrEmptyVersions_FailsWithValidation()
    {
        Gen.Select(Gen.Bool, Gen.DateTimeOffset).Sample(
            sample =>
            {
                CareProfileVersion[]? versions =
                    sample.Item1 ? null : Array.Empty<CareProfileVersion>();

                Result<CareProfileVersion> result =
                    CareVersionResolver.ResolveForProduction(versions, sample.Item2);

                Assert.True(result.IsFailure);
                Assert.Equal(ErrorCode.Validation, result.Error.Code);
            },
            iter: Iterations);
    }
}
