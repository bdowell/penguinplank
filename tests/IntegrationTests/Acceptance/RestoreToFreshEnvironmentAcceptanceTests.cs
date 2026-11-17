using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PenguinPlank.Application.Media.UseCases;
using PenguinPlank.Domain.Media;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Acceptance scenario #14 (requirements A6 §8.3, §8.4): a backup set (database + media) taken from
/// a running environment restores into a <b>fresh</b> environment, and the restored system passes a
/// read/write smoke check.
/// </summary>
/// <remarks>
/// <para>
/// The test drives the real operational scripts from <c>deploy/backup</c>, exactly as the runbook
/// (section 6) prescribes. It builds a source database from the actual EF migrations, runs the real
/// API against it, stores a product and an uploaded image, and takes a backup set with
/// <c>Backup-PenguinPlank.ps1</c> (including the verified off-host copy). It then restores the
/// <em>off-host</em> copy with <c>Restore-PenguinPlank.ps1</c> into a new database name and an empty
/// media folder, starts a second API host on the restored environment, and completes the smoke check:
/// Owner sign-in, product read, media download of the same bytes, a successful write, a clean media
/// integrity report, and no pending migrations.
/// </para>
/// <para>
/// It needs a real local SQL Server instance (the same <c>localhost\SQLEXPRESS</c> the other
/// integration tests use), <c>sqlcmd</c>, Windows PowerShell and <c>robocopy</c>, because the
/// scripts shell out to them and SQL Server must read and write the backup files on this machine. On
/// any other platform the test is skipped (see <see cref="WindowsRestoreDrillFactAttribute"/>).
/// </para>
/// </remarks>
public sealed class RestoreToFreshEnvironmentAcceptanceTests
{
    private const string SqlInstance = "localhost\\SQLEXPRESS";
    private const string ServerConnectionString =
        "Server=localhost\\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True";
    private const string CsrfHeaderName = "X-CSRF-TOKEN";
    private const string SeededProductName = "Restore drill board";
    private static readonly TimeSpan s_scriptTimeout = TimeSpan.FromMinutes(5);

    // A valid 1x1 PNG: it carries the PNG magic-byte signature the upload policy requires.
    private static readonly byte[] s_pngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [WindowsRestoreDrillFact]
    public async Task RestoredBackupSet_InFreshEnvironment_PassesReadWriteSmokeCheckAsync()
    {
        // Arrange: unique names and folders for the source environment, backup areas, and target.
        string runId = Guid.NewGuid().ToString("N");
        string sourceDatabase = $"PenguinPlank_RestoreSrc_{runId}";
        string restoredDatabase = $"PenguinPlank_RestoreDst_{runId}";
        string workRoot = Path.Combine(Path.GetTempPath(), $"pp-restore-drill-{runId}");
        string sourceMedia = Path.Combine(workRoot, "source-media");
        string backupRoot = Path.Combine(workRoot, "backup");
        string offHostRoot = Path.Combine(workRoot, "offhost");
        string restoredMedia = Path.Combine(workRoot, "restored-media");

        try
        {
            Directory.CreateDirectory(sourceMedia);
            Directory.CreateDirectory(backupRoot);
            Directory.CreateDirectory(offHostRoot);
            await GrantSqlServerAccessAsync(workRoot);

            await CreateMigratedDatabaseAsync(sourceDatabase);
            Guid mediaAssetId = await SeedSourceEnvironmentAsync(sourceDatabase, sourceMedia);

            // Act 1: take a backup set with the real script (database, media, verified off-host copy).
            await RunScriptAsync(
                "Backup-PenguinPlank.ps1",
                $"-SqlServerInstance {Quote(SqlInstance)} -Database {Quote(sourceDatabase)} "
                    + $"-MediaRoot {Quote(sourceMedia)} -BackupRoot {Quote(backupRoot)} "
                    + $"-OffHostRoot {Quote(offHostRoot)}");

            string[] offHostSets = Directory.GetDirectories(offHostRoot, "ppbackup-*");
            string offHostSet = Assert.Single(offHostSets);

            // Act 2: restore the off-host copy into a new database name and an empty media folder.
            await RunScriptAsync(
                "Restore-PenguinPlank.ps1",
                $"-BackupSetPath {Quote(offHostSet)} -SqlServerInstance {Quote(SqlInstance)} "
                    + $"-TargetDatabase {Quote(restoredDatabase)} -TargetMediaRoot {Quote(restoredMedia)} "
                    + "-Confirm:$false");

            // Assert: the restored environment passes the runbook smoke check.
            await AssertRestoredEnvironmentAsync(restoredDatabase, restoredMedia, mediaAssetId);
        }
        finally
        {
            await DropDatabaseAsync(sourceDatabase);
            await DropDatabaseAsync(restoredDatabase);
            if (Directory.Exists(workRoot))
            {
                Directory.Delete(workRoot, recursive: true);
            }
        }
    }

    // =====================================================================================
    // Source environment: real migrations + real API, so the backup holds genuine data.
    // =====================================================================================

    private static async Task<Guid> SeedSourceEnvironmentAsync(string database, string mediaRoot)
    {
        await using RestoreEnvironmentApiFactory source = new(ConnectionStringFor(database), mediaRoot);
        using HttpClient owner = source.CreateApiClient();

        string bootstrapCsrf = await AcceptanceApiSession.FetchCsrfTokenAsync(owner);
        using (HttpRequestMessage bootstrap = new(HttpMethod.Post, "/api/v1/owner-bootstrap"))
        {
            bootstrap.Headers.Add(CsrfHeaderName, bootstrapCsrf);
            using HttpResponseMessage bootstrapResponse = await owner.SendAsync(bootstrap);
            Assert.True(bootstrapResponse.IsSuccessStatusCode, "The source Owner bootstrap failed.");
        }

        await AcceptanceApiSession.SignInAsync(
            owner,
            RestoreEnvironmentApiFactory.OwnerEmail,
            RestoreEnvironmentApiFactory.OwnerPassword);

        await CreateProductAsync(owner, SeededProductName);
        return await UploadPngAsync(owner);
    }

    // =====================================================================================
    // Restored environment: the smoke check from runbook section 6, step 6.
    // =====================================================================================

    private static async Task AssertRestoredEnvironmentAsync(string database, string mediaRoot, Guid mediaAssetId)
    {
        string connectionString = ConnectionStringFor(database);

        // Runbook step 5: migrations newer than the backup are applied; the committed model is the
        // same as the backup's, so nothing is pending and applying is a no-op.
        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>().UseSqlServer(connectionString).Options;
        await using (PenguinPlankDbContext context = new(options))
        {
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            await context.Database.MigrateAsync();
        }

        await using RestoreEnvironmentApiFactory restored = new(connectionString, mediaRoot);
        using HttpClient owner = restored.CreateApiClient();

        // The restored Identity data lets the Owner sign in without re-running the bootstrap.
        await AcceptanceApiSession.SignInAsync(
            owner,
            RestoreEnvironmentApiFactory.OwnerEmail,
            RestoreEnvironmentApiFactory.OwnerPassword);

        // Read: the product created before the backup is listed.
        using (HttpResponseMessage list = await owner.GetAsync("/api/v1/products"))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            JsonElement body = await ReadJsonAsync(list);
            string?[] names = body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("name").GetString())
                .ToArray();
            Assert.Contains(SeededProductName, names);
        }

        // Media: the restored file downloads with exactly the bytes that were uploaded.
        using (HttpResponseMessage download = await owner.GetAsync($"/api/v1/media/{mediaAssetId}/content"))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(s_pngBytes, await download.Content.ReadAsByteArrayAsync());
        }

        // Write: a new product is accepted by the restored database.
        Guid writtenProductId = await CreateProductAsync(owner, "Post-restore board");
        using (HttpResponseMessage reread = await owner.GetAsync($"/api/v1/products/{writtenProductId}"))
        {
            Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
        }

        // Integrity: no media row is missing its file and no stray file exists.
        MediaIntegrityReport report = await DetectMediaIntegrityAsync(restored);
        Assert.Empty(report.MissingFileKeys);
        Assert.Empty(report.OrphanFileKeys);

        // The detector is live against the restored environment: removing a file is reported.
        string restoredFile = Assert.Single(Directory.GetFiles(mediaRoot));
        File.Delete(restoredFile);
        MediaIntegrityReport afterDelete = await DetectMediaIntegrityAsync(restored);
        Assert.Equal([Path.GetFileName(restoredFile)], afterDelete.MissingFileKeys);
    }

    private static async Task<MediaIntegrityReport> DetectMediaIntegrityAsync(RestoreEnvironmentApiFactory host)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        DetectMediaIntegrityIssuesUseCase useCase =
            scope.ServiceProvider.GetRequiredService<DetectMediaIntegrityIssuesUseCase>();
        return await useCase.ExecuteAsync(CancellationToken.None);
    }

    // =====================================================================================
    // API drivers.
    // =====================================================================================

    private static async Task<Guid> CreateProductAsync(HttpClient owner, string name)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new { name, publicDescription = "Restore drill product." }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using HttpResponseMessage response = await owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> UploadPngAsync(HttpClient owner)
    {
        using ByteArrayContent file = new(s_pngBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using MultipartFormDataContent form = new() { { file, "file", "pixel.png" } };

        using HttpResponseMessage response = await owner.PostAsync("/api/v1/media", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("mediaAssetId").GetGuid();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }

    // =====================================================================================
    // SQL Server and script plumbing.
    // =====================================================================================

    private static string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = database }.ConnectionString;

    private static async Task CreateMigratedDatabaseAsync(string database)
    {
        await using (SqlConnection connection = new(ServerConnectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand command = new($"CREATE DATABASE [{database}]", connection);
            await command.ExecuteNonQueryAsync();
        }

        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>()
                .UseSqlServer(ConnectionStringFor(database))
                .Options;
        await using PenguinPlankDbContext context = new(options);
        await context.Database.MigrateAsync();
    }

    private static async Task DropDatabaseAsync(string database)
    {
        SqlConnection.ClearAllPools();
        await using SqlConnection connection = new(ServerConnectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(
            $"IF DB_ID(N'{database}') IS NOT NULL BEGIN "
            + $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{database}]; END",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Lets the SQL Server service account write backups into, and read them from, the work folder
    /// (the runbook requires the same for the real staging folder).
    /// </summary>
    private static async Task GrantSqlServerAccessAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        (int exitCode, string output) = await RunProcessAsync(
            "icacls.exe",
            [folder, "/grant", "NT SERVICE\\MSSQL$SQLEXPRESS:(OI)(CI)F", "/T"]);
        Assert.True(exitCode == 0, $"icacls failed (exit {exitCode}): {output}");
    }

    private static async Task RunScriptAsync(string scriptName, string arguments)
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "deploy", "backup", scriptName);
        string command = $"& {Quote(scriptPath)} {arguments}; exit $LASTEXITCODE";

        (int exitCode, string output) = await RunProcessAsync(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]);

        Assert.True(exitCode == 0, $"{scriptName} failed (exit {exitCode}): {output}");
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource timeout = new(s_scriptTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} did not finish within {s_scriptTimeout}.");
        }

        return (process.ExitCode, await standardOutput + await standardError);
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Finds the repository root (the folder holding <c>deploy/backup</c>). It searches upward from the
    /// test output folder first, then from this source file, so a build into an output folder outside
    /// the repository (for example <c>--artifacts-path</c> under %TEMP%) still finds the scripts.
    /// </summary>
    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        string?[] startFolders = [AppContext.BaseDirectory, Path.GetDirectoryName(sourceFilePath)];
        foreach (string? start in startFolders)
        {
            for (DirectoryInfo? directory = string.IsNullOrEmpty(start) ? null : new(start);
                directory is not null;
                directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "deploy", "backup", "Restore-PenguinPlank.ps1")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new InvalidOperationException("Could not locate the repository root containing deploy/backup.");
    }

    /// <summary>
    /// The in-process API bound to one database and one media folder, used for both the source
    /// environment and the restored one. The caller owns the database and folder.
    /// </summary>
    private sealed class RestoreEnvironmentApiFactory : WebApplicationFactory<Program>
    {
        public const string OwnerEmail = "owner@penguinplank.test";
        public const string OwnerPassword = "Owner!Bootstrap#2025";

        private readonly string _connectionString;
        private readonly string _mediaRoot;

        public RestoreEnvironmentApiFactory(string connectionString, string mediaRoot)
        {
            _connectionString = connectionString;
            _mediaRoot = mediaRoot;
        }

        public HttpClient CreateApiClient()
        {
            HttpClient client = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment(Environments.Production);

            // UseSetting (not ConfigureAppConfiguration) so the connection string is visible while
            // the composition root registers services; see AcceptanceApiFactory for the rationale.
            builder.UseSetting(
                $"ConnectionStrings:{PenguinPlank.Api.Composition.ServiceCollectionExtensions.DatabaseConnectionName}",
                _connectionString);
            builder.UseSetting("OwnerBootstrap:Email", OwnerEmail);
            builder.UseSetting("OwnerBootstrap:InitialPassword", OwnerPassword);
            builder.UseSetting("FileStore:RootPath", _mediaRoot);
            builder.UseSetting("FileStore:ImageMaxBytes", "20971520");
            builder.UseSetting("FileStore:VideoMaxBytes", "209715200");
        }
    }
}

/// <summary>
/// Marks the restore drill as a Windows-only test: the scripts under test use Windows PowerShell,
/// <c>robocopy</c>, <c>sqlcmd</c> and a local SQL Server that reads and writes files on this host.
/// The test is skipped elsewhere (for example the Linux CI job) rather than failing for a reason
/// unrelated to the code under test.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsRestoreDrillFactAttribute : FactAttribute
{
    /// <summary>Initializes the attribute, skipping the test when the platform cannot run the scripts.</summary>
    public WindowsRestoreDrillFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "The restore drill runs the Windows PowerShell backup/restore scripts against a local SQL Server.";
        }
        else if (!IsOnPath("sqlcmd.exe"))
        {
            Skip = "The restore drill needs sqlcmd.exe on PATH (the backup and restore scripts call it).";
        }
    }

    private static bool IsOnPath(string fileName) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory.Trim(), fileName)));
}
