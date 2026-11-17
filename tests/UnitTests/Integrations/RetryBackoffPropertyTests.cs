using CsCheck;
using PenguinPlank.Application.Integrations;

namespace UnitTests.Integrations;

/// <summary>
/// Property 23 (requirement R11, 3.10): <em>Retry backoff is bounded and increasing.</em>
/// <see cref="RetryBackoffPolicy"/> is a pure function of its options and an injected jitter
/// function, so these properties need no clock, database, or application startup
/// (coding-standards sections 1 and 7). For any valid options and any jitter sequence:
/// <list type="number">
///   <item>The schedule has at most <see cref="RetryBackoffOptions.MaxAttempts"/> attempts,
///   numbered consecutively from one.</item>
///   <item>The base delay is non-decreasing in the attempt number, before jitter.</item>
///   <item>Every base delay respects the configured cap and the rate-limit floor.</item>
///   <item>Every jittered delay stays within the base delay and the jitter ratio, and never falls
///   below <see cref="RetryBackoffOptions.MinimumInterval"/>.</item>
/// </list>
/// </summary>
public class RetryBackoffPropertyTests
{
    private const int Iterations = 1000;

    private static readonly Gen<RetryBackoffOptions> s_genOptions =
        Gen.Select(
                Gen.Int[0, RetryBackoffPolicy.AttemptCeiling],
                Gen.Int[1, 5_000],
                Gen.Int[0, 600_000],
                Gen.Double[0, 1],
                Gen.Int[0, 700_000])
            .Select(
                (maxAttempts, initialMilliseconds, extraMilliseconds, jitterRatio, minimumMilliseconds) =>
                    new RetryBackoffOptions(
                        maxAttempts,
                        TimeSpan.FromMilliseconds(initialMilliseconds),
                        TimeSpan.FromMilliseconds(initialMilliseconds + extraMilliseconds),
                        jitterRatio,
                        TimeSpan.FromMilliseconds(minimumMilliseconds)));

    /// <summary>Raw jitter samples, deliberately including out-of-range values that must be clamped.</summary>
    private static readonly Gen<double[]> s_genJitterSamples =
        Gen.Double[-0.5, 1.5].Array[RetryBackoffPolicy.AttemptCeiling, RetryBackoffPolicy.AttemptCeiling];

    private static Func<int, double> JitterFrom(double[] samples) => attemptNumber => samples[attemptNumber - 1];

    [Fact]
    public void Schedule_AnyOptions_HasAtMostMaxAttemptsNumberedConsecutively()
    {
        Gen.Select(s_genOptions, s_genJitterSamples)
            .Sample(
                (options, samples) =>
                {
                    IReadOnlyList<RetryAttempt> schedule = RetryBackoffPolicy.Schedule(options, JitterFrom(samples));

                    Assert.True(schedule.Count <= options.MaxAttempts);
                    Assert.Equal(options.MaxAttempts, schedule.Count);
                    for (int index = 0; index < schedule.Count; index++)
                    {
                        Assert.Equal(index + 1, schedule[index].AttemptNumber);
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void Schedule_AnyOptions_BaseDelayIsNonDecreasingBeforeJitter()
    {
        Gen.Select(s_genOptions, s_genJitterSamples)
            .Sample(
                (options, samples) =>
                {
                    IReadOnlyList<RetryAttempt> schedule = RetryBackoffPolicy.Schedule(options, JitterFrom(samples));

                    for (int index = 1; index < schedule.Count; index++)
                    {
                        Assert.True(schedule[index].BaseDelay >= schedule[index - 1].BaseDelay);
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void Schedule_AnyOptions_BaseDelayRespectsCapAndRateLimitFloor()
    {
        Gen.Select(s_genOptions, s_genJitterSamples)
            .Sample(
                (options, samples) =>
                {
                    TimeSpan ceiling = options.MaxDelay > options.MinimumInterval ? options.MaxDelay : options.MinimumInterval;

                    foreach (RetryAttempt attempt in RetryBackoffPolicy.Schedule(options, JitterFrom(samples)))
                    {
                        Assert.True(attempt.BaseDelay >= options.InitialDelay);
                        Assert.True(attempt.BaseDelay >= options.MinimumInterval);
                        Assert.True(attempt.BaseDelay <= ceiling);
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void Schedule_AnyJitter_DelayStaysWithinJitterRatioAndAboveRateLimitFloor()
    {
        Gen.Select(s_genOptions, s_genJitterSamples)
            .Sample(
                (options, samples) =>
                {
                    foreach (RetryAttempt attempt in RetryBackoffPolicy.Schedule(options, JitterFrom(samples)))
                    {
                        double baseMilliseconds = attempt.BaseDelay.TotalMilliseconds;
                        double upperBound = baseMilliseconds * (1 + options.JitterRatio);

                        Assert.True(attempt.Delay >= attempt.BaseDelay);
                        Assert.True(attempt.Delay >= options.MinimumInterval);
                        Assert.True(attempt.Delay.TotalMilliseconds <= upperBound + 1);
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void Schedule_AboveAttemptCeiling_IsRejected()
    {
        var options = new RetryBackoffOptions(
            RetryBackoffPolicy.AttemptCeiling + 1,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMinutes(1),
            0.5,
            TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => RetryBackoffPolicy.Schedule(options, _ => 0));
    }
}
