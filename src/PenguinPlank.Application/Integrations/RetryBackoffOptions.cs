namespace PenguinPlank.Application.Integrations;

/// <summary>Configured limits for retrying a transient external failure.</summary>
/// <param name="MaxAttempts">Maximum number of retry attempts. Zero means no retries.</param>
/// <param name="InitialDelay">Base delay before the first retry. Must be positive.</param>
/// <param name="MaxDelay">Upper bound for the exponential base delay. Must be at least <paramref name="InitialDelay"/>.</param>
/// <param name="JitterRatio">Largest fraction of the base delay that jitter may add. Between zero and one.</param>
/// <param name="MinimumInterval">Provider rate-limit floor: no retry is scheduled sooner than this after the previous one.</param>
public sealed record RetryBackoffOptions(
    int MaxAttempts,
    TimeSpan InitialDelay,
    TimeSpan MaxDelay,
    double JitterRatio,
    TimeSpan MinimumInterval);
