namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>
/// An <see cref="IRetryScheduler"/> backed by <see cref="RetryBackoffPolicy"/>. The schedule is
/// computed once at construction from validated options and an injected jitter source.
/// </summary>
public sealed class BackoffRetryScheduler : IRetryScheduler
{
    private readonly IReadOnlyList<RetryAttempt> _schedule;

    /// <summary>Creates the scheduler.</summary>
    /// <param name="options">The retry limits.</param>
    /// <param name="jitter">Returns a value in [0, 1] for a one-based retry number.</param>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its allowed range.</exception>
    public BackoffRetryScheduler(RetryBackoffOptions options, Func<int, double> jitter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jitter);

        _schedule = RetryBackoffPolicy.Schedule(options, jitter);
    }

    /// <inheritdoc />
    public int MaxTotalAttempts => _schedule.Count + 1;

    /// <inheritdoc />
    public TimeSpan? NextDelay(int attemptsMade)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptsMade, 1);

        return attemptsMade <= _schedule.Count ? _schedule[attemptsMade - 1].Delay : null;
    }
}
