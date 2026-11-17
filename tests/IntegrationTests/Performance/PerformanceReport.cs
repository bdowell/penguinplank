using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace IntegrationTests.Performance;

/// <summary>The hardware and software the measurement ran on (the declared Reference_Environment).</summary>
/// <param name="OperatingSystem">The OS description.</param>
/// <param name="ProcessorCount">The logical processor count visible to the runtime.</param>
/// <param name="TotalMemoryBytes">The memory available to the runtime, in bytes.</param>
/// <param name="RuntimeVersion">The .NET runtime description.</param>
/// <param name="SqlServerVersion">The first line of <c>@@VERSION</c> of the SQL Server instance under test.</param>
public sealed record ReferenceEnvironmentInfo(
    string OperatingSystem,
    int ProcessorCount,
    long TotalMemoryBytes,
    string RuntimeVersion,
    string SqlServerVersion)
{
    /// <summary>Captures the current process environment together with the supplied SQL Server version text.</summary>
    /// <param name="sqlServerVersion">The SQL Server version string read from the instance under test.</param>
    /// <returns>The captured environment.</returns>
    public static ReferenceEnvironmentInfo Capture(string sqlServerVersion)
    {
        ArgumentNullException.ThrowIfNull(sqlServerVersion);

        return new ReferenceEnvironmentInfo(
            RuntimeInformation.OSDescription,
            Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            RuntimeInformation.FrameworkDescription,
            sqlServerVersion);
    }
}

/// <summary>Everything the report renderer needs; a plain value so rendering is a pure function.</summary>
/// <param name="Environment">The Reference_Environment the run executed on.</param>
/// <param name="Dataset">The seeded dataset shape.</param>
/// <param name="Users">The number of concurrent simulated users.</param>
/// <param name="IterationsPerUser">The workload iterations each user ran.</param>
/// <param name="Samples">Every recorded sample, including failures.</param>
/// <param name="Evaluations">The target evaluations for reads and mutations.</param>
public sealed record PerformanceRun(
    ReferenceEnvironmentInfo Environment,
    CatalogDatasetSpec Dataset,
    int Users,
    int IterationsPerUser,
    IReadOnlyList<LatencySample> Samples,
    IReadOnlyList<TargetEvaluation> Evaluations);

/// <summary>Renders a <see cref="PerformanceRun"/> as a Markdown report suitable for committing under <c>docs/performance</c>.</summary>
public static class PerformanceReportFormatter
{
    private const double BytesPerGibibyte = 1024d * 1024d * 1024d;

    /// <summary>Formats the run.</summary>
    /// <param name="run">The run to render.</param>
    /// <returns>The Markdown text.</returns>
    public static string Format(PerformanceRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        CultureInfo culture = CultureInfo.InvariantCulture;
        StringBuilder text = new();
        text.AppendLine("# Catalog latency run");
        text.AppendLine();
        text.AppendLine("## Reference_Environment");
        text.AppendLine(culture, $"- OS: {run.Environment.OperatingSystem}");
        text.AppendLine(culture, $"- Logical processors: {run.Environment.ProcessorCount}");
        text.AppendLine(culture, $"- Memory available to runtime: {run.Environment.TotalMemoryBytes / BytesPerGibibyte:F1} GiB");
        text.AppendLine(culture, $"- Runtime: {run.Environment.RuntimeVersion}");
        text.AppendLine(culture, $"- SQL Server: {run.Environment.SqlServerVersion}");
        text.AppendLine();
        text.AppendLine("## Dataset and workload");
        text.AppendLine(culture, $"- Products: {run.Dataset.ProductCount}; variants: {run.Dataset.VariantCount}; pieces: {run.Dataset.PieceCount}");
        text.AppendLine(culture, $"- Concurrent users: {run.Users}; iterations per user: {run.IterationsPerUser}");
        text.AppendLine(culture, $"- Samples: {run.Samples.Count} ({run.Samples.Count(sample => !sample.Succeeded)} failed)");
        text.AppendLine();
        text.AppendLine("## Results (successful requests, milliseconds)");
        text.AppendLine();
        text.AppendLine("| Operation | Kind | n | mean | p50 | p95 | p99 | max |");
        text.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");

        foreach (IGrouping<(string Operation, OperationKind Kind), LatencySample> group in run.Samples
                     .Where(sample => sample.Succeeded)
                     .GroupBy(sample => (sample.Operation, sample.Kind))
                     .OrderBy(group => group.Key.Kind)
                     .ThenBy(group => group.Key.Operation, StringComparer.Ordinal))
        {
            LatencySummary summary = LatencyStatistics.Summarize([.. group.Select(sample => sample.Elapsed)]);
            text.AppendLine(culture, $"| {group.Key.Operation} | {group.Key.Kind} | {summary.Count} | {Ms(summary.Mean)} | {Ms(summary.P50)} | {Ms(summary.P95)} | {Ms(summary.P99)} | {Ms(summary.Max)} |");
        }

        text.AppendLine();
        text.AppendLine("## Target evaluation");
        foreach (TargetEvaluation evaluation in run.Evaluations)
        {
            text.AppendLine(culture, $"- {(evaluation.Passed ? "PASS" : "FAIL")}: {evaluation.Message}");
        }

        return text.ToString();
    }

    private static string Ms(TimeSpan value) => value.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
}
