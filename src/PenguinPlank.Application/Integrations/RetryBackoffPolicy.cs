namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Pure bounded exponential backoff with jitter (requirement 3.10). Jitter is injected as a function
/// so the schedule is deterministic under test.
/// </summary>
public static class RetryBackoffPolicy
{
    /// <summary>Upper bound on <see cref="RetryBackoffOptions.MaxAttempts"/> accepted by <see cref="Schedule"/>.</summary>
    public const int AttemptCeiling = 20;

    /// <summary>Computes the base delay (before jitter) for a one-based retry number.</summary>
    /// <param name="attemptNumber">One-based retry number.</param>
    /// <param name="options">Validated retry options.</param>
    /// <returns>The exponential delay capped at <see cref="RetryBackoffOptions.MaxDelay"/> and raised to the rate-limit floor.</returns>
    public static TimeSpan ComputeBaseDelay(int attemptNumber, RetryBackoffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptNumber, 1);

        double exponential = options.InitialDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attemptNumber - 1, 62));
        double capped = Math.Min(exponential, options.MaxDelay.TotalMilliseconds);
        double floored = Math.Max(capped, options.MinimumInterval.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(floored);
    }

    /// <summary>Builds the retry schedule.</summary>
    /// <param name="options">Retry limits.</param>
    /// <param name="jitter">Returns a value in [0, 1] for a one-based retry number; out-of-range values are clamped.</param>
    /// <returns>At most <see cref="RetryBackoffOptions.MaxAttempts"/> attempts, in order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its allowed range.</exception>
    public static IReadOnlyList<RetryAttempt> Schedule(RetryBackoffOptions options, Func<int, double> jitter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jitter);
        Validate(options);

        var attempts = new List<RetryAttempt>(options.MaxAttempts);
        for (int attemptNumber = 1; attemptNumber <= options.MaxAttempts; attemptNumber++)
        {
            TimeSpan baseDelay = ComputeBaseDelay(attemptNumber, options);
            double sample = jitter(attemptNumber);
            double fraction = double.IsNaN(sample) ? 0 : Math.Clamp(sample, 0, 1);
            double jitteredMilliseconds = baseDelay.TotalMilliseconds * (1 + (options.JitterRatio * fraction));
            attempts.Add(new RetryAttempt(attemptNumber, baseDelay, TimeSpan.FromMilliseconds(jitteredMilliseconds)));
        }

        return attempts;
    }

    private static void Validate(RetryBackoffOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxAttempts, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaxAttempts, AttemptCeiling, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.InitialDelay, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxDelay, options.InitialDelay, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumInterval, TimeSpan.Zero, nameof(options));
        if (!(options.JitterRatio >= 0 && options.JitterRatio <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "JitterRatio must be between 0 and 1.");
        }
    }
}
