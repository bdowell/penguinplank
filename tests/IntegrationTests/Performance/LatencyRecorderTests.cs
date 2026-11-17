using Microsoft.Extensions.Time.Testing;

namespace IntegrationTests.Performance;

/// <summary>Database-free tests of the timing recorder and the Markdown report (requirement A9 §11.3).</summary>
public sealed class LatencyRecorderTests
{
    [Fact]
    public async Task MeasureAsync_OperationTakes120Milliseconds_RecordsThatElapsedTimeAsync()
    {
        FakeTimeProvider clock = new();
        LatencyRecorder recorder = new(clock);

        await recorder.MeasureAsync("GET /x", OperationKind.Read, () =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(120));
            return Task.FromResult(true);
        });

        LatencySample sample = Assert.Single(recorder.Samples);
        Assert.Equal(TimeSpan.FromMilliseconds(120), sample.Elapsed);
        Assert.True(sample.Succeeded);
        Assert.Equal(OperationKind.Read, sample.Kind);
    }

    [Fact]
    public async Task SuccessfulLatencies_FailedSample_IsExcludedAndCountedAsync()
    {
        FakeTimeProvider clock = new();
        LatencyRecorder recorder = new(clock);

        await recorder.MeasureAsync("GET /ok", OperationKind.Read, () =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(10));
            return Task.FromResult(true);
        });
        await recorder.MeasureAsync("GET /bad", OperationKind.Read, () => Task.FromResult(false));

        Assert.Single(recorder.SuccessfulLatencies(OperationKind.Read));
        Assert.Empty(recorder.SuccessfulLatencies(OperationKind.Mutation));
        Assert.Equal(1, recorder.FailureCount());
    }

    [Fact]
    public async Task MeasureAsync_OperationThrows_RecordsFailureAndPropagatesAsync()
    {
        LatencyRecorder recorder = new(new FakeTimeProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            recorder.MeasureAsync("POST /x", OperationKind.Mutation, () => throw new InvalidOperationException("boom")));

        Assert.Equal(1, recorder.FailureCount());
    }

    [Fact]
    public void Format_RunWithSamples_IncludesEnvironmentDatasetAndVerdicts()
    {
        LatencySample[] samples =
        [
            new("GET /a", OperationKind.Read, TimeSpan.FromMilliseconds(20), true),
            new("POST /b", OperationKind.Mutation, TimeSpan.FromMilliseconds(80), true),
        ];
        TargetEvaluation[] evaluations =
        [
            CatalogLatencyTargets.Evaluate(OperationKind.Read, LatencyStatistics.Summarize([samples[0].Elapsed])),
        ];
        PerformanceRun run = new(
            new ReferenceEnvironmentInfo("TestOS", 8, 16L * 1024 * 1024 * 1024, ".NET test", "SQL Server test"),
            CatalogDatasetSpec.Reference,
            10,
            40,
            samples,
            evaluations);

        string report = PerformanceReportFormatter.Format(run);

        Assert.Contains("Logical processors: 8", report, StringComparison.Ordinal);
        Assert.Contains("variants: 5000; pieces: 50000", report, StringComparison.Ordinal);
        Assert.Contains("| GET /a | Read | 1 |", report, StringComparison.Ordinal);
        Assert.Contains("PASS: Read p95", report, StringComparison.Ordinal);
    }
}
