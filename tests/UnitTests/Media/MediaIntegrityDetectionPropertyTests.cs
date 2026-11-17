using CsCheck;
using PenguinPlank.Domain.Media;

namespace UnitTests.Media;

/// <summary>
/// Property 25 (requirement 8.7 / A6): <em>Orphan and missing media detection.</em> For any set of
/// stored media files and MediaAsset metadata, the detector reports exactly the metadata keys whose
/// backing file is missing and exactly the stored files with no referencing metadata, and nothing
/// else. Pure value test; no storage, database, or application startup is needed.
/// </summary>
public sealed class MediaIntegrityDetectionPropertyTests
{
    private const int Iterations = 1000;

    /// <summary>
    /// Generates a pair of key lists drawn from a small alphabet so overlap, duplicates, and
    /// case-only differences occur frequently.
    /// </summary>
    private static readonly Gen<(string[] Metadata, string[] Stored)> s_genKeySets =
        Gen.Select(
            Gen.String[Gen.Char["aAbB1"], 1, 3].Array[0, 12],
            Gen.String[Gen.Char["aAbB1"], 1, 3].Array[0, 12]);

    [Fact]
    public void Detect_AnyKeySets_ReportsExactlyMissingAndOrphanKeys()
    {
        s_genKeySets.Sample(
            sets =>
            {
                HashSet<string> metadata = sets.Metadata.ToHashSet(StringComparer.Ordinal);
                HashSet<string> stored = sets.Stored.ToHashSet(StringComparer.Ordinal);

                MediaIntegrityReport report = MediaIntegrityDetector.Detect(sets.Metadata, sets.Stored);

                string[] expectedMissing = metadata.Except(stored).Order(StringComparer.Ordinal).ToArray();
                string[] expectedOrphans = stored.Except(metadata).Order(StringComparer.Ordinal).ToArray();

                Assert.Equal(expectedMissing, report.MissingFileKeys);
                Assert.Equal(expectedOrphans, report.OrphanFileKeys);
            },
            iter: Iterations);
    }

    [Fact]
    public void Detect_AnyKeySets_NeverReportsAKeyPresentOnBothSides()
    {
        s_genKeySets.Sample(
            sets =>
            {
                MediaIntegrityReport report = MediaIntegrityDetector.Detect(sets.Metadata, sets.Stored);

                Assert.DoesNotContain(report.MissingFileKeys, key => sets.Stored.Contains(key, StringComparer.Ordinal));
                Assert.DoesNotContain(report.OrphanFileKeys, key => sets.Metadata.Contains(key, StringComparer.Ordinal));
                Assert.Equal(
                    report.IsClean,
                    report.MissingFileKeys.Count == 0 && report.OrphanFileKeys.Count == 0);
            },
            iter: Iterations);
    }

    [Fact]
    public void Detect_IdenticalKeySets_IsClean()
    {
        s_genKeySets.Sample(
            sets =>
            {
                Assert.True(MediaIntegrityDetector.Detect(sets.Metadata, sets.Metadata.Reverse()).IsClean);
            },
            iter: Iterations);
    }
}
