using CsCheck;

namespace IntegrationTests.Performance;

/// <summary>Database-free tests of the pure latency statistics and target evaluation (requirement A9 §11.2, §11.3).</summary>
public sealed class LatencyStatisticsTests
{
    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static IReadOnlyCollection<TimeSpan> OneToHundred() =>
        [.. Enumerable.Range(1, 100).Select(Ms)];

    [Fact]
    public void Percentile_HundredOrderedSamples_Returns95thSampleAtP95()
    {
        TimeSpan p95 = LatencyStatistics.Percentile(OneToHundred(), 95m);

        Assert.Equal(Ms(95), p95);
    }

    [Fact]
    public void Percentile_TwentySamples_RankIsExactlyNineteenAtP95()
    {
        // 0.95 * 20 is exactly 19; binary floating point could push the rank to 20.
        IReadOnlyCollection<TimeSpan> samples = [.. Enumerable.Range(1, 20).Select(Ms)];

        Assert.Equal(Ms(19), LatencyStatistics.Percentile(samples, 95m));
    }

    [Fact]
    public void Percentile_UnorderedInput_SortsBeforeRanking()
    {
        IReadOnlyCollection<TimeSpan> samples = [Ms(900), Ms(10), Ms(500), Ms(100), Ms(300)];

        // Sorted: 10, 100, 300, 500, 900; nearest rank for p50 is ceil(2.5) = 3.
        Assert.Equal(Ms(300), LatencyStatistics.Percentile(samples, 50m));
        Assert.Equal(Ms(900), LatencyStatistics.Percentile(samples, 100m));
    }

    [Fact]
    public void Percentile_SingleSample_ReturnsThatSampleForAnyPercentile()
    {
        Assert.Equal(Ms(42), LatencyStatistics.Percentile([Ms(42)], 1m));
        Assert.Equal(Ms(42), LatencyStatistics.Percentile([Ms(42)], 99.9m));
    }

    [Fact]
    public void Percentile_EmptySamples_Throws()
    {
        Assert.Throws<ArgumentException>(() => LatencyStatistics.Percentile([], 95m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Percentile_OutOfRange_Throws(double percentile)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LatencyStatistics.Percentile([Ms(1)], (decimal)percentile));
    }

    [Fact]
    public void Summarize_KnownSamples_ReportsExtremesMeanAndPercentiles()
    {
        LatencySummary summary = LatencyStatistics.Summarize(OneToHundred());

        Assert.Equal(100, summary.Count);
        Assert.Equal(Ms(1), summary.Min);
        Assert.Equal(Ms(100), summary.Max);
        Assert.Equal(Ms(50), summary.P50);
        Assert.Equal(Ms(95), summary.P95);
        Assert.Equal(Ms(99), summary.P99);
        Assert.Equal(TimeSpan.FromMilliseconds(50.5), summary.Mean);
    }

    [Fact]
    public void Percentile_AnySamples_ReturnsObservedValueAndIsMonotonic()
    {
        Gen.Select(Gen.Int[0, 100_000].List[1, 200], Gen.Int[1, 100], Gen.Int[1, 100])
            .Sample(
                input =>
                {
                    TimeSpan[] samples = [.. input.Item1.Select(value => TimeSpan.FromTicks(value))];
                    decimal low = Math.Min(input.Item2, input.Item3);
                    decimal high = Math.Max(input.Item2, input.Item3);

                    TimeSpan atLow = LatencyStatistics.Percentile(samples, low);
                    TimeSpan atHigh = LatencyStatistics.Percentile(samples, high);

                    Assert.Contains(atLow, samples);
                    Assert.True(atLow <= atHigh);
                    Assert.Equal(samples.Max(), LatencyStatistics.Percentile(samples, 100m));
                },
                iter: 200);
    }

    [Fact]
    public void Evaluate_ReadP95JustUnderLimit_Passes()
    {
        LatencySummary summary = LatencyStatistics.Summarize([.. Enumerable.Repeat(Ms(499), 20)]);

        TargetEvaluation evaluation = CatalogLatencyTargets.Evaluate(OperationKind.Read, summary);

        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void Evaluate_ReadP95AtLimit_Fails()
    {
        LatencySummary summary = LatencyStatistics.Summarize([.. Enumerable.Repeat(Ms(500), 20)]);

        Assert.False(CatalogLatencyTargets.Evaluate(OperationKind.Read, summary).Passed);
    }

    [Fact]
    public void Evaluate_MutationP95OverOneSecond_Fails()
    {
        LatencySummary summary = LatencyStatistics.Summarize([.. Enumerable.Repeat(Ms(1001), 20)]);

        Assert.False(CatalogLatencyTargets.Evaluate(OperationKind.Mutation, summary).Passed);
    }

    [Fact]
    public void Evaluate_SlowReadIsStillUnderMutationLimit_UsesPerKindLimit()
    {
        LatencySummary summary = LatencyStatistics.Summarize([.. Enumerable.Repeat(Ms(700), 20)]);

        Assert.False(CatalogLatencyTargets.Evaluate(OperationKind.Read, summary).Passed);
        Assert.True(CatalogLatencyTargets.Evaluate(OperationKind.Mutation, summary).Passed);
    }

    [Fact]
    public void Evaluate_NoSamples_FailsBecauseNothingWasMeasured()
    {
        Assert.False(CatalogLatencyTargets.Evaluate(OperationKind.Read, null).Passed);
    }

    [Fact]
    public void ReferenceDataset_MatchesDocumentedPlanningEnvelope()
    {
        CatalogDatasetSpec reference = CatalogDatasetSpec.Reference;

        Assert.Equal(CatalogLatencyTargets.PlanningVariants, reference.VariantCount);
        Assert.Equal(CatalogLatencyTargets.PlanningPieces, reference.PieceCount);
        Assert.Equal(10, CatalogLatencyTargets.ConcurrentUsers);
    }

    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 3, 1)]
    [InlineData(1, 2, 0)]
    [InlineData(1, 2, 1000)]
    public void Validate_InconsistentSpec_Throws(int products, int variantsPerProduct, int piecesPerVariant)
    {
        CatalogDatasetSpec spec = new(products, variantsPerProduct, piecesPerVariant);

        Assert.Throws<ArgumentException>(spec.Validate);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData(" TRUE ", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsEnabled_EnvironmentValue_OptsIntoPerformanceSuiteOnlyWhenExplicit(string? value, bool expected)
    {
        Assert.Equal(expected, PerformanceFactAttribute.IsEnabled(value));
    }
}
