namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>Timing and sizing settings for the Background_Worker.</summary>
/// <param name="PollInterval">The wait between processing cycles. Must be positive.</param>
/// <param name="LeaseDuration">How long a lease lasts before another worker may reclaim the item. Must be positive.</param>
/// <param name="BatchSize">The largest number of items leased per cycle. Must be positive.</param>
public sealed record WorkerOptions(TimeSpan PollInterval, TimeSpan LeaseDuration, int BatchSize)
{
    /// <summary>The Phase A defaults.</summary>
    public static WorkerOptions Default { get; } = new(
        PollInterval: TimeSpan.FromSeconds(30),
        LeaseDuration: TimeSpan.FromMinutes(5),
        BatchSize: 20);

    /// <summary>Checks that every setting is within range.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A setting is not positive.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(PollInterval, TimeSpan.Zero, nameof(PollInterval));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LeaseDuration, TimeSpan.Zero, nameof(LeaseDuration));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(BatchSize, 0, nameof(BatchSize));
    }
}
