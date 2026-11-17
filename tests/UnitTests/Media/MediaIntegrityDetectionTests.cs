using PenguinPlank.Application.Media;
using PenguinPlank.Application.Media.UseCases;
using PenguinPlank.Domain.Media;

namespace UnitTests.Media;

/// <summary>
/// Unit tests for missing/orphan media detection (task 17.1, requirement 8.7 / A6).
/// </summary>
public sealed class MediaIntegrityDetectionTests
{
    [Fact]
    public void Detect_MatchingSets_ReportsNothing()
    {
        MediaIntegrityReport report = MediaIntegrityDetector.Detect(Keys("a b"), Keys("b a"));

        Assert.True(report.IsClean);
    }

    [Fact]
    public void Detect_MetadataWithoutFileAndFileWithoutMetadata_ReportsExactlyThoseKeys()
    {
        MediaIntegrityReport report = MediaIntegrityDetector.Detect(
            Keys("shared missing-b missing-a"),
            Keys("orphan-b shared orphan-a"));

        Assert.Equal(Keys("missing-a missing-b"), report.MissingFileKeys);
        Assert.Equal(Keys("orphan-a orphan-b"), report.OrphanFileKeys);
    }

    [Fact]
    public void Detect_EmptyInputs_ReportsNothing()
    {
        Assert.True(MediaIntegrityDetector.Detect(Keys(string.Empty), Keys(string.Empty)).IsClean);
    }

    [Fact]
    public void Detect_DuplicateKeys_ReportsEachOnce()
    {
        MediaIntegrityReport report = MediaIntegrityDetector.Detect(Keys("m m"), Keys("o o"));

        Assert.Equal(Keys("m"), report.MissingFileKeys);
        Assert.Equal(Keys("o"), report.OrphanFileKeys);
    }

    [Fact]
    public void Detect_KeysDifferingByCase_AreDistinct()
    {
        MediaIntegrityReport report = MediaIntegrityDetector.Detect(Keys("Key"), Keys("key"));

        Assert.Equal(Keys("Key"), report.MissingFileKeys);
        Assert.Equal(Keys("key"), report.OrphanFileKeys);
    }

    [Fact]
    public async Task ExecuteAsync_ReadersReturnKeys_ReportsDetectorResultAsync()
    {
        var useCase = new DetectMediaIntegrityIssuesUseCase(
            new FakeKeyReader(Keys("kept gone")),
            new FakeKeyEnumerator(Keys("kept stray")));

        MediaIntegrityReport report = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Keys("gone"), report.MissingFileKeys);
        Assert.Equal(Keys("stray"), report.OrphanFileKeys);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsAsync()
    {
        var useCase = new DetectMediaIntegrityIssuesUseCase(
            new FakeKeyReader(Keys(string.Empty)),
            new FakeKeyEnumerator(Keys(string.Empty)));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => useCase.ExecuteAsync(cancellation.Token));
    }

    private static string[] Keys(string spaceSeparated) =>
        spaceSeparated.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private sealed class FakeKeyReader : IMediaStorageKeyReader
    {
        private readonly IReadOnlyList<string> _keys;

        public FakeKeyReader(IReadOnlyList<string> keys) => _keys = keys;

        public Task<IReadOnlyList<string>> GetAllStorageKeysAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_keys);
        }
    }

    private sealed class FakeKeyEnumerator : IStoredFileKeyEnumerator
    {
        private readonly IReadOnlyList<string> _keys;

        public FakeKeyEnumerator(IReadOnlyList<string> keys) => _keys = keys;

        public Task<IReadOnlyList<string>> ListStoredKeysAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_keys);
        }
    }
}
