namespace IntegrationTests.Performance;

/// <summary>
/// Pure latency statistics for the catalog performance harness (requirement A9 §11.2, §11.3).
/// </summary>
/// <remarks>
/// <para>
/// The percentile uses the <b>nearest-rank</b> method: for <c>n</c> ordered samples the
/// <c>p</c>-th percentile is the sample at one-based rank <c>ceil(p / 100 * n)</c>. The result is
/// always an actually observed latency (no interpolation), which is the conservative choice for a
/// "p95 under the target" assertion. The rank is computed with <see cref="decimal"/> arithmetic so
/// <c>95 / 100 * 20</c> is exactly 19 rather than a binary-floating-point neighbour of 19.
/// </para>
/// <para>
/// The functions take explicit inputs and touch no clock, file or database, so they are testable
/// without starting the application (coding-standards §1).
/// </para>
/// </remarks>
public static class LatencyStatistics
{
    private const decimal MinimumPercentileExclusive = 0m;
    private const decimal MaximumPercentile = 100m;

    /// <summary>Computes the nearest-rank percentile of the supplied samples.</summary>
    /// <param name="samples">The observed latencies, in any order. Must not be empty.</param>
    /// <param name="percentile">The percentile in the range (0, 100].</param>
    /// <returns>The observed latency at the nearest rank.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="samples"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="samples"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="percentile"/> is outside (0, 100].</exception>
    public static TimeSpan Percentile(IReadOnlyCollection<TimeSpan> samples, decimal percentile)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one latency sample is required.", nameof(samples));
        }

        if (percentile <= MinimumPercentileExclusive || percentile > MaximumPercentile)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentile),
                percentile,
                "The percentile must be greater than 0 and at most 100.");
        }

        TimeSpan[] ordered = [.. samples.Order()];
        return ordered[RankIndex(ordered.Length, percentile)];
    }

    /// <summary>Summarizes a set of latencies into count, extremes, mean, and key percentiles.</summary>
    /// <param name="samples">The observed latencies. Must not be empty.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="samples"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="samples"/> is empty.</exception>
    public static LatencySummary Summarize(IReadOnlyCollection<TimeSpan> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one latency sample is required.", nameof(samples));
        }

        TimeSpan[] ordered = [.. samples.Order()];
        long meanTicks = (long)ordered.Average(sample => (double)sample.Ticks);

        return new LatencySummary(
            Count: ordered.Length,
            Min: ordered[0],
            Mean: TimeSpan.FromTicks(meanTicks),
            P50: ordered[RankIndex(ordered.Length, 50m)],
            P95: ordered[RankIndex(ordered.Length, 95m)],
            P99: ordered[RankIndex(ordered.Length, 99m)],
            Max: ordered[^1]);
    }

    private static int RankIndex(int count, decimal percentile)
    {
        int rank = (int)Math.Ceiling(percentile * count / MaximumPercentile);
        return Math.Clamp(rank, 1, count) - 1;
    }
}

/// <summary>A summary of observed latencies for one class of operation.</summary>
/// <param name="Count">The number of samples.</param>
/// <param name="Min">The fastest sample.</param>
/// <param name="Mean">The arithmetic mean.</param>
/// <param name="P50">The nearest-rank median.</param>
/// <param name="P95">The nearest-rank 95th percentile.</param>
/// <param name="P99">The nearest-rank 99th percentile.</param>
/// <param name="Max">The slowest sample.</param>
public sealed record LatencySummary(
    int Count,
    TimeSpan Min,
    TimeSpan Mean,
    TimeSpan P50,
    TimeSpan P95,
    TimeSpan P99,
    TimeSpan Max);
