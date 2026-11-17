namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>
/// Decides whether and when a failed item is retried (requirements 3.10, 3.16). Jitter and rate
/// limits are injected into the implementation, so the worker stays deterministic under test.
/// </summary>
public interface IRetryScheduler
{
    /// <summary>The most attempts any item receives: the first attempt plus the bounded retries.</summary>
    int MaxTotalAttempts { get; }

    /// <summary>Computes the wait before the next attempt.</summary>
    /// <param name="attemptsMade">The number of attempts already made, at least one.</param>
    /// <returns>The delay, or <see langword="null"/> when attempts are exhausted.</returns>
    TimeSpan? NextDelay(int attemptsMade);
}
