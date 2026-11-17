namespace IntegrationTests.Performance;

/// <summary>One timed request observed by the harness.</summary>
/// <param name="Operation">A stable label for the request shape, e.g. <c>GET /variants (page)</c>.</param>
/// <param name="Kind">Whether the request was a read or a mutation.</param>
/// <param name="Elapsed">The wall-clock time from sending the request to reading the full response body.</param>
/// <param name="Succeeded">Whether the response was the expected success status.</param>
public sealed record LatencySample(string Operation, OperationKind Kind, TimeSpan Elapsed, bool Succeeded);

/// <summary>
/// Thread-safe collector that times operations with an injected <see cref="TimeProvider"/> so the
/// timing logic is deterministic under test (coding-standards §2).
/// </summary>
public sealed class LatencyRecorder
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly List<LatencySample> _samples = [];

    /// <summary>Initializes a recorder that reads elapsed time from <paramref name="timeProvider"/>.</summary>
    /// <param name="timeProvider">The time source.</param>
    public LatencyRecorder(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Gets a snapshot of every sample recorded so far.</summary>
    public IReadOnlyList<LatencySample> Samples
    {
        get
        {
            lock (_gate)
            {
                return [.. _samples];
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="operation"/>, records how long it took, and records whether it reported
    /// success. A thrown exception is recorded as a failed sample and then propagated unchanged.
    /// </summary>
    /// <param name="operationName">The label for the request shape.</param>
    /// <param name="kind">Whether the request is a read or a mutation.</param>
    /// <param name="operation">The request to time; returns <see langword="true"/> when the expected success status was received.</param>
    /// <returns>A task that completes when the operation has been recorded.</returns>
    public async Task MeasureAsync(string operationName, OperationKind kind, Func<Task<bool>> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);

        long start = _timeProvider.GetTimestamp();
        bool succeeded = false;
        try
        {
            succeeded = await operation().ConfigureAwait(false);
        }
        finally
        {
            TimeSpan elapsed = _timeProvider.GetElapsedTime(start);
            LatencySample sample = new(operationName, kind, elapsed, succeeded);
            lock (_gate)
            {
                _samples.Add(sample);
            }
        }
    }

    /// <summary>Returns the elapsed times of the successful samples of one operation class.</summary>
    /// <param name="kind">The operation class.</param>
    /// <returns>The successful latencies; failed requests are excluded so an error response cannot masquerade as a fast one.</returns>
    public IReadOnlyList<TimeSpan> SuccessfulLatencies(OperationKind kind)
    {
        lock (_gate)
        {
            return [.. _samples.Where(sample => sample.Kind == kind && sample.Succeeded).Select(sample => sample.Elapsed)];
        }
    }

    /// <summary>Returns the number of failed samples.</summary>
    /// <returns>The failed-sample count.</returns>
    public int FailureCount()
    {
        lock (_gate)
        {
            return _samples.Count(sample => !sample.Succeeded);
        }
    }
}
