namespace IntegrationTests.Performance;

/// <summary>The class of catalog API operation a latency sample belongs to.</summary>
public enum OperationKind
{
    /// <summary>A read (<c>GET</c>) request.</summary>
    Read,

    /// <summary>A mutating (<c>POST</c>/<c>PATCH</c>) request.</summary>
    Mutation,
}

/// <summary>The outcome of comparing observed latencies with a documented target.</summary>
/// <param name="Kind">The operation class that was evaluated.</param>
/// <param name="Passed">Whether the observed p95 is strictly under the documented limit.</param>
/// <param name="Message">A one-line human-readable explanation suitable for an assertion message.</param>
public sealed record TargetEvaluation(OperationKind Kind, bool Passed, string Message);

/// <summary>
/// The documented Phase A planning envelope and latency targets (requirement A9 §11.1, §11.2),
/// plus the pure rule that compares observed latencies with them.
/// </summary>
/// <remarks>
/// <para>
/// The targets are measured on the declared Reference_Environment (see
/// <c>docs/performance/catalog-performance.md</c>) and exclude media transfer and external network
/// time. The mutation target is evaluated at the 95th percentile as well, so a single outlier does
/// not fail the run while a consistently slow write does; the maximum is still reported. No
/// threshold beyond those in requirement 11.2 is introduced (requirement 11.4).
/// </para>
/// </remarks>
public static class CatalogLatencyTargets
{
    /// <summary>The planning-envelope number of concurrent internal users.</summary>
    public const int ConcurrentUsers = 10;

    /// <summary>The planning-envelope number of product variants.</summary>
    public const int PlanningVariants = 5_000;

    /// <summary>The planning-envelope number of <c>ProductPiece</c> records.</summary>
    public const int PlanningPieces = 50_000;

    /// <summary>
    /// The planning-envelope number of stock lines. Stock is a Phase B+ workflow, so it is
    /// documented as a system target but not seeded or measured in Phase A (requirement 11.4).
    /// </summary>
    public const int PlanningStockLines = 250_000;

    /// <summary>The exclusive upper bound for the p95 of catalog API reads.</summary>
    public static readonly TimeSpan ReadP95Limit = TimeSpan.FromMilliseconds(500);

    /// <summary>The exclusive upper bound for the p95 of catalog API mutations.</summary>
    public static readonly TimeSpan MutationP95Limit = TimeSpan.FromSeconds(1);

    /// <summary>Returns the documented exclusive p95 limit for the operation class.</summary>
    /// <param name="kind">The operation class.</param>
    /// <returns>The limit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined operation class.</exception>
    public static TimeSpan LimitFor(OperationKind kind) => kind switch
    {
        OperationKind.Read => ReadP95Limit,
        OperationKind.Mutation => MutationP95Limit,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown operation kind."),
    };

    /// <summary>
    /// Evaluates observed latencies against the documented target. An empty sample set fails: a run
    /// that measured nothing is not evidence that the target is met.
    /// </summary>
    /// <param name="kind">The operation class the samples belong to.</param>
    /// <param name="summary">The summary of the observed latencies, or <see langword="null"/> when none were observed.</param>
    /// <returns>The evaluation.</returns>
    public static TargetEvaluation Evaluate(OperationKind kind, LatencySummary? summary)
    {
        TimeSpan limit = LimitFor(kind);

        if (summary is null || summary.Count == 0)
        {
            return new TargetEvaluation(kind, false, $"No {kind} samples were observed.");
        }

        bool passed = summary.P95 < limit;
        string verdict = passed ? "meets" : "EXCEEDS";
        string message =
            $"{kind} p95 {summary.P95.TotalMilliseconds:F1} ms {verdict} the < {limit.TotalMilliseconds:F0} ms target " +
            $"(n={summary.Count}, max {summary.Max.TotalMilliseconds:F1} ms).";
        return new TargetEvaluation(kind, passed, message);
    }
}
