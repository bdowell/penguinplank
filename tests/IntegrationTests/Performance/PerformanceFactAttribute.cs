namespace IntegrationTests.Performance;

/// <summary>
/// Marks a test as part of the opt-in performance suite. The test is skipped unless the
/// <see cref="EnableVariable"/> environment variable is set to <c>1</c> or <c>true</c>, so the default
/// fast test run never seeds 50,000 rows or runs a timed load test. CI opts in either by setting the
/// variable or by selecting the trait, e.g. <c>--filter "Category=Performance"</c>; the default run
/// can exclude the trait with <c>--filter "Category!=Performance"</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PerformanceFactAttribute : FactAttribute
{
    /// <summary>The environment variable that enables the performance suite.</summary>
    public const string EnableVariable = "PENGUINPLANK_RUN_PERFORMANCE";

    /// <summary>The xUnit trait category applied to performance tests.</summary>
    public const string TraitCategory = "Performance";

    /// <summary>Initializes the attribute, skipping the test unless the suite is enabled.</summary>
    public PerformanceFactAttribute()
    {
        if (!IsEnabled(Environment.GetEnvironmentVariable(EnableVariable)))
        {
            Skip = $"Performance suite is opt-in. Set {EnableVariable}=1 to run it (see docs/performance/catalog-performance.md).";
        }
    }

    /// <summary>Decides whether the supplied environment-variable value enables the suite.</summary>
    /// <param name="value">The raw variable value, or <see langword="null"/> when unset.</param>
    /// <returns><see langword="true"/> for <c>1</c> or <c>true</c> (case-insensitive).</returns>
    public static bool IsEnabled(string? value) =>
        value is not null
        && (value.Trim() == "1" || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
}
