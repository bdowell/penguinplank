using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PenguinPlank.Infrastructure.Persistence;

namespace BrowserTests.Hosting;

/// <summary>
/// Starts the real API in-process against a <b>real isolated SQL Server database</b> built from the
/// actual migrations, for the browser smoke tests (coding-standards §3, §7). It follows the same
/// pattern as the integration suite's acceptance factory: the database is created and migrated
/// before the host starts, the connection string and the operator Owner-bootstrap settings are
/// supplied through host settings so the composition root sees them at registration time, and the
/// database is dropped on disposal. EF InMemory is never used.
/// </summary>
/// <remarks>
/// The SQL Server instance defaults to the local <c>SQLEXPRESS</c> instance with Windows
/// authentication, matching the integration suite. When the <c>ConnectionStrings__SqlServer</c>
/// environment variable is set (as the CI workflow does), that server is used instead and only its
/// catalog is replaced with the per-run database name.
/// </remarks>
public sealed class BrowserApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The environment variable that overrides the SQL Server connection (used by CI).</summary>
    public const string ServerConnectionVariable = "ConnectionStrings__SqlServer";

    /// <summary>The operator-supplied Owner-bootstrap email the first Owner is created with.</summary>
    public const string OwnerEmail = "owner@penguinplank.test";

    /// <summary>The operator-supplied Owner-bootstrap password; it satisfies the Identity policy.</summary>
    public const string OwnerPassword = "Owner!Bootstrap#2025";

    private const string DefaultServerConnectionString =
        "Server=localhost\\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True";

    private const string ImageMaxBytes = "20971520";
    private const string VideoMaxBytes = "209715200";
    private const string TestLoginPermitLimit = "1000";

    private readonly string _databaseName = $"PenguinPlank_BrowserTests_{Guid.NewGuid():N}";

    // The media file store must live outside any web root; a throwaway temp directory keeps the
    // uploads of one run isolated and is removed on teardown.
    private readonly string _mediaRootPath =
        Path.Combine(Path.GetTempPath(), $"pp-media-browser-{Guid.NewGuid():N}");

    private string _serverConnectionString = DefaultServerConnectionString;

    /// <summary>The connection string targeting the isolated per-run database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Creates the isolated database and applies the committed migrations to it.</summary>
    /// <remarks>
    /// A failure here (for example, SQL Server unreachable) surfaces with the real exception rather
    /// than falling back to a non-relational provider.
    /// </remarks>
    /// <returns>A task that completes when the database is migrated.</returns>
    public async Task InitializeAsync()
    {
        string configured =
            Environment.GetEnvironmentVariable(ServerConnectionVariable) is { Length: > 0 } fromEnvironment
                ? fromEnvironment
                : DefaultServerConnectionString;

        // Server-level commands (create/drop) run against master; the app uses the per-run catalog.
        SqlConnectionStringBuilder serverBuilder = new(configured) { InitialCatalog = "master" };
        _serverConnectionString = serverBuilder.ConnectionString;

        SqlConnectionStringBuilder builder = new(configured)
        {
            InitialCatalog = _databaseName,
        };
        ConnectionString = builder.ConnectionString;

        await ExecuteOnServerAsync($"CREATE DATABASE [{_databaseName}]");

        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

        await using PenguinPlankDbContext context = new(options);
        await context.Database.MigrateAsync();
    }

    /// <summary>Stops the host and drops the isolated database and media root.</summary>
    /// <returns>A task that completes when teardown finishes.</returns>
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        SqlConnection.ClearAllPools();
        await ExecuteOnServerAsync(
            $"IF DB_ID(N'{_databaseName}') IS NOT NULL " +
            "BEGIN " +
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{_databaseName}]; " +
            "END");

        if (Directory.Exists(_mediaRootPath))
        {
            Directory.Delete(_mediaRootPath, recursive: true);
        }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);

        // Production behavior: the dev-only OpenAPI mapping stays off.
        builder.UseEnvironment(Environments.Production);

        // Keep warnings and errors only, so a failing run's output stays readable. A provider-specific
        // rule takes precedence over the generic levels in appsettings.json.
        builder.ConfigureLogging(logging => logging.AddFilter<ConsoleLoggerProvider>(null, LogLevel.Warning));

        // UseSetting (not a late ConfigureAppConfiguration source) so the composition root sees the
        // connection string while it registers services; see the integration suite's factory.
        builder.UseSetting(
            $"ConnectionStrings:{PenguinPlank.Api.Composition.ServiceCollectionExtensions.DatabaseConnectionName}",
            ConnectionString);
        builder.UseSetting("OwnerBootstrap:Email", OwnerEmail);
        builder.UseSetting("OwnerBootstrap:InitialPassword", OwnerPassword);

        // The media store is validated on host start and is used for real by the upload test.
        builder.UseSetting("FileStore:RootPath", _mediaRootPath);
        builder.UseSetting("FileStore:ImageMaxBytes", ImageMaxBytes);
        builder.UseSetting("FileStore:VideoMaxBytes", VideoMaxBytes);

        // Every sign-in in the run (each browser context plus the seed client) arrives from the same
        // in-process client address, so the production default of 10 login attempts per minute would
        // throttle the suite itself. Raise only this test host's limit; the limiter stays enabled.
        builder.UseSetting("RateLimiting:LoginPermitLimit", TestLoginPermitLimit);
    }

    private async Task ExecuteOnServerAsync(string commandText)
    {
        await using SqlConnection connection = new(_serverConnectionString);
        await connection.OpenAsync();

        await using SqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }
}
