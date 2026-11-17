namespace PenguinPlank.Application.Integrations;

/// <summary>One scheduled retry.</summary>
/// <param name="AttemptNumber">One-based retry number.</param>
/// <param name="BaseDelay">Delay before jitter. Non-decreasing in <paramref name="AttemptNumber"/>.</param>
/// <param name="Delay">Delay to wait after jitter. Never below the rate-limit floor.</param>
public sealed record RetryAttempt(int AttemptNumber, TimeSpan BaseDelay, TimeSpan Delay);
