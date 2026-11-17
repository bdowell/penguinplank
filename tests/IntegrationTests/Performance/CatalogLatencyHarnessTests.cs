using System.Collections.Concurrent;
using System.Globalization;
using IntegrationTests.Acceptance;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

/// <summary>
/// The opt-in catalog latency verification harness (requirement A9 §11.1–§11.3). It builds an
/// isolated SQL Server database from the <b>actual migrations</b>, seeds the representative dataset,
/// signs in ten Owner sessions through the real cookie flow, drives the repeatable workload from ten
/// concurrent users against the in-process API, and asserts the documented targets: p95 reads under
/// 500 ms and p95 mutations under one second.
/// </summary>
/// <remarks>
/// <para>
/// The test is skipped unless <c>PENGUINPLANK_RUN_PERFORMANCE=1</c> (see
/// <see cref="PerformanceFactAttribute"/>) and carries the <c>Category=Performance</c> trait. It
/// never connects to anything but the throw-away database <see cref="AcceptanceApiFactory"/> creates
/// and drops. Optional knobs: <c>PENGUINPLANK_PERF_SCALE=smoke</c> for a small dataset,
/// <c>PENGUINPLANK_PERF_ITERATIONS</c> (default 40) and <c>PENGUINPLANK_PERF_REPORT_DIR</c>
/// (default: the temp directory). Method and procedure: <c>docs/performance/catalog-performance.md</c>.
/// </para>
/// </remarks>
[Collection(CatalogPerformanceTestGroup.Name)]
[Trait("Category", PerformanceFactAttribute.TraitCategory)]
public sealed class CatalogLatencyHarnessTests
{
    private const int DefaultIterationsPerUser = 40;
    private const int WarmUpIterationsPerUser = 5;
    private const int RandomSeed = 20260101;
    private const string CsrfHeaderName = "X-CSRF-TOKEN";

    private readonly ITestOutputHelper _output;

    public CatalogLatencyHarnessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [PerformanceFact]
    public async Task CatalogApi_TenConcurrentUsersOnSeededDataset_MeetsDocumentedLatencyTargetsAsync()
    {
        // Arrange: isolated, migrated database; seed; authenticated sessions.
        bool smoke = string.Equals(
            Environment.GetEnvironmentVariable("PENGUINPLANK_PERF_SCALE"), "smoke", StringComparison.OrdinalIgnoreCase);
        CatalogDatasetSpec spec = smoke ? CatalogDatasetSpec.Smoke : CatalogDatasetSpec.Reference;
        int iterations = int.TryParse(
            Environment.GetEnvironmentVariable("PENGUINPLANK_PERF_ITERATIONS"),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int configured) && configured > 0
            ? configured
            : DefaultIterationsPerUser;
        int users = CatalogLatencyTargets.ConcurrentUsers;

        AcceptanceApiFactory factory = new();
        try
        {
            await factory.InitializeAsync();
            await CatalogDatasetSeeder.SeedAsync(factory.ConnectionString, spec, CancellationToken.None);
            CatalogDatasetSample sample = await CatalogDatasetSeeder.LoadSampleAsync(factory.ConnectionString, CancellationToken.None);
            string sqlVersion = await ReadSqlServerVersionAsync(factory.ConnectionString);

            // Login throttling protects the anonymous sign-in surface, not the catalog; lift it for the
            // harness so ten sessions can be established from one address. Catalog endpoints are untouched.
            // Per-command Information logging to the console would itself dominate request latency
            // under ten concurrent users, so the harness logs warnings and above only.
            using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("RateLimiting:LoginPermitLimit", "1000");
                builder.UseSetting("Logging:LogLevel:Default", "Warning");
            });

            await BootstrapOwnerAsync(host);
            List<(HttpClient Client, string CsrfToken)> sessions = [];
            for (int index = 0; index < users; index++)
            {
                sessions.Add(await SignInOwnerAsync(host));
            }

            try
            {
                // Warm-up (JIT, connection pool, EF model/query caches) is excluded from the measurement.
                LatencyRecorder warmUp = new(TimeProvider.System);
                await RunUsersAsync(sessions, warmUp, spec, sample, runId: "warm", WarmUpIterationsPerUser, new ConcurrentQueue<string>());

                // Act
                LatencyRecorder recorder = new(TimeProvider.System);
                ConcurrentQueue<string> failures = new();
                await RunUsersAsync(sessions, recorder, spec, sample, runId: "run", iterations, failures);

                // Assert
                IReadOnlyList<TargetEvaluation> evaluations =
                [
                    CatalogLatencyTargets.Evaluate(OperationKind.Read, Summarize(recorder, OperationKind.Read)),
                    CatalogLatencyTargets.Evaluate(OperationKind.Mutation, Summarize(recorder, OperationKind.Mutation)),
                ];

                PerformanceRun run = new(
                    ReferenceEnvironmentInfo.Capture(sqlVersion), spec, users, iterations, recorder.Samples, evaluations);
                string report = PerformanceReportFormatter.Format(run);
                await WriteReportAsync(report);

                Assert.True(
                    recorder.FailureCount() == 0,
                    $"{recorder.FailureCount()} request(s) did not return the expected status, so the latencies are not valid evidence. First failures: "
                    + string.Join(" | ", failures.Take(5)));
                Assert.All(evaluations, evaluation => Assert.True(evaluation.Passed, evaluation.Message));
            }
            finally
            {
                foreach ((HttpClient client, _) in sessions)
                {
                    client.Dispose();
                }
            }
        }
        finally
        {
            // Resolves to AcceptanceApiFactory.DisposeAsync(), which also drops the throw-away database.
            await factory.DisposeAsync();
        }
    }

    private static LatencySummary? Summarize(LatencyRecorder recorder, OperationKind kind)
    {
        IReadOnlyList<TimeSpan> latencies = recorder.SuccessfulLatencies(kind);
        return latencies.Count == 0 ? null : LatencyStatistics.Summarize(latencies);
    }

    private static async Task RunUsersAsync(
        List<(HttpClient Client, string CsrfToken)> sessions,
        LatencyRecorder recorder,
        CatalogDatasetSpec spec,
        CatalogDatasetSample sample,
        string runId,
        int iterations,
        ConcurrentQueue<string> failures)
    {
        TaskCompletionSource startGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Task> userTasks = [];

        for (int userIndex = 0; userIndex < sessions.Count; userIndex++)
        {
            CatalogWorkload workload = new(
                sessions[userIndex].Client,
                sessions[userIndex].CsrfToken,
                recorder,
                new Random(RandomSeed + userIndex),
                userIndex,
                sessions.Count,
                runId,
                spec,
                sample,
                failures);
            userTasks.Add(RunUserAsync(workload, iterations, startGate.Task));
        }

        // Release every user at once so the measured window really has ten concurrent sessions.
        startGate.SetResult();
        await Task.WhenAll(userTasks);
    }

    private static async Task RunUserAsync(CatalogWorkload workload, int iterations, Task startGate)
    {
        await startGate;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            await workload.RunIterationAsync(iteration);
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static async Task BootstrapOwnerAsync(WebApplicationFactory<Program> host)
    {
        using HttpClient client = CreateClient(host);
        string csrf = await AcceptanceApiSession.FetchCsrfTokenAsync(client);
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/owner-bootstrap");
        request.Headers.Add(CsrfHeaderName, csrf);
        using HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<(HttpClient Client, string CsrfToken)> SignInOwnerAsync(WebApplicationFactory<Program> host)
    {
        HttpClient client = CreateClient(host);
        await AcceptanceApiSession.SignInAsync(client, AcceptanceApiFactory.OwnerEmail, AcceptanceApiFactory.OwnerPassword);

        // The antiforgery request token is bound to the signed-in identity, so fetch it after login.
        string csrf = await AcceptanceApiSession.FetchCsrfTokenAsync(client);
        return (client, csrf);
    }

    private static async Task<string> ReadSqlServerVersionAsync(string connectionString)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new("SELECT @@VERSION", connection);
        string version = (string?)await command.ExecuteScalarAsync() ?? "unknown";
        return version.Split('\n')[0].Trim();
    }

    private async Task WriteReportAsync(string report)
    {
        string directory = Environment.GetEnvironmentVariable("PENGUINPLANK_PERF_REPORT_DIR") is { Length: > 0 } configured
            ? configured
            : Path.GetTempPath();
        Directory.CreateDirectory(directory);

        string stamp = TimeProvider.System.GetUtcNow().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        string path = Path.Combine(directory, $"catalog-latency-{stamp}.md");
        await File.WriteAllTextAsync(path, report);

        _output.WriteLine(report);
        _output.WriteLine($"Report written to {path}");
    }
}

/// <summary>
/// The xUnit collection for the performance suite. Parallelization is disabled so no other test
/// class competes for CPU or SQL Server while latency is being measured.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CatalogPerformanceTestGroup
{
    /// <summary>The collection name shared by the performance test classes.</summary>
    public const string Name = "CatalogPerformance";
}
