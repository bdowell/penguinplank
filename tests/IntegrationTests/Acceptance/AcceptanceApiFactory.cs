using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Spins up the real API in-process with <see cref="WebApplicationFactory{TEntryPoint}"/> backed by
/// a <b>real isolated SQL Server database</b> built from the actual migrations, so the
/// acceptance-scenario tests (#10/#11/#12) exercise the end-to-end HTTP pipeline — authentication,
/// the field-level Owner/Staff projection, and the public/private content rules — against the same
/// relational schema production uses (coding-standards §3, §7).
/// </summary>
/// <remarks>
/// <para>
/// The factory owns a per-run database named <c>PenguinPlank_IntegrationTests_{guid}</c> so a run
/// never collides with the schema-constraint collection's fixture or another acceptance run. It
/// creates the database and applies the committed migration <em>before</em> the host starts
/// (EF InMemory is never used), then overrides the <c>PenguinPlankDatabase</c> connection string so
/// the in-process API runs against that database. If SQL Server is unreachable the create/migrate
/// step throws and the whole collection fails with the real exception rather than silently falling
/// back to a non-relational provider.
/// </para>
/// <para>
/// The factory also supplies the operator-provided Owner-bootstrap configuration (an email and a
/// policy-satisfying initial password) so a test can provision the first Owner through the real
/// one-time bootstrap endpoint, then create Staff and sign in through the genuine Identity cookie
/// flow. The database is dropped on disposal, leaving no artifact behind.
/// </para>
/// </remarks>
public sealed class AcceptanceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ServerConnectionString =
        "Server=localhost\\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True";

    private readonly string _databaseName =
        $"PenguinPlank_IntegrationTests_{Guid.NewGuid():N}";

    // A throwaway media-store root OUTSIDE any web root, satisfying the file-store options
    // validation that runs on host start. The acceptance scenarios never upload or download a
    // binary, so nothing is written here; it is removed on teardown.
    private readonly string _mediaRootPath =
        Path.Combine(Path.GetTempPath(), $"pp-media-acc-{Guid.NewGuid():N}");

    /// <summary>The operator-supplied Owner-bootstrap email the first Owner is created with.</summary>
    public const string OwnerEmail = "owner@penguinplank.test";

    /// <summary>
    /// The operator-supplied Owner-bootstrap password. It satisfies the configured Identity policy
    /// (length ≥ 12, upper/lower/digit/non-alphanumeric, ≥ 4 unique characters).
    /// </summary>
    public const string OwnerPassword = "Owner!Bootstrap#2025";

    /// <summary>The connection string targeting the isolated per-run database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Creates the isolated database and applies the committed migration to it. Any failure here
    /// (for example, SQL Server not reachable) surfaces as a collection failure with the real
    /// exception rather than a silent fallback to a non-relational provider (coding-standards §7).
    /// </summary>
    public async Task InitializeAsync()
    {
        SqlConnectionStringBuilder builder = new(ServerConnectionString)
        {
            InitialCatalog = _databaseName,
        };
        ConnectionString = builder.ConnectionString;

        await CreateDatabaseAsync();

        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

        await using PenguinPlankDbContext context = new(options);
        await context.Database.MigrateAsync();
    }

    /// <summary>Drops the isolated database on teardown, removing every artifact created during the run.</summary>
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        await using SqlConnection connection = new(ServerConnectionString);
        await connection.OpenAsync();

        string sql =
            $"IF DB_ID(N'{_databaseName}') IS NOT NULL " +
            $"BEGIN " +
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{_databaseName}]; " +
            $"END";

        SqlConnection.ClearAllPools();

        await using SqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();

        // Remove the throwaway media-store root if the host created it.
        if (Directory.Exists(_mediaRootPath))
        {
            Directory.Delete(_mediaRootPath, recursive: true);
        }
    }

    /// <summary>
    /// Builds a context bound to the isolated database so a test can seed the few rows it needs
    /// (for example, a care profile and a variant that reference it) or assert the persisted state
    /// directly, exactly as the catalog-persistence integration tests do.
    /// </summary>
    public PenguinPlankDbContext CreateContext()
    {
        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

        return new PenguinPlankDbContext(options);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> whose base address is <c>https://localhost</c> so the
    /// hardened application cookie (<c>SecurePolicy=Always</c>) is accepted and re-sent; the handler
    /// follows no redirects, so the configured <c>OnRedirectToLogin → 401</c> is observed verbatim.
    /// </summary>
    public HttpClient CreateApiClient()
    {
        HttpClient client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private async Task CreateDatabaseAsync()
    {
        await using SqlConnection connection = new(ServerConnectionString);
        await connection.OpenAsync();

        await using SqlCommand command =
            new($"CREATE DATABASE [{_databaseName}]", connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);

        // Run as Production so the dev-only OpenAPI mapping stays off and the behavior matches a
        // deployed host; the composition root reads configuration either way.
        builder.UseEnvironment(Environments.Production);

        // Point the real composition root at the isolated SQL Server database and supply the
        // operator Owner-bootstrap configuration, so the in-process API runs against real SQL
        // Server (not EF InMemory) and a test can provision the first Owner through the genuine
        // bootstrap endpoint.
        //
        // These values are supplied through <see cref="IWebHostBuilder.UseSetting"/> rather than a
        // layered <c>ConfigureAppConfiguration</c> source on purpose. The production
        // <c>Program.cs</c> reads the connection string <em>while it registers services</em>
        // (<c>AddOwnerBootstrap</c>/<c>AddPersistence</c> only wire the database-dependent
        // services when a connection string is configured). A <c>ConfigureAppConfiguration</c>
        // source added here is layered on <em>after</em> the <see cref="WebApplicationBuilder"/>
        // has already built its configuration and run those top-level registrations, so the
        // connection string would be invisible at registration time and the one-time
        // <c>IOwnerBootstrapper</c> would silently go unregistered — making the first-run bootstrap
        // POST fail its minimal-API binding with an opaque 400. <c>UseSetting</c> writes into the
        // host configuration the builder reads up front, so the registrations observe the same
        // configuration a deployed host does.
        builder.UseSetting(
            $"ConnectionStrings:{PenguinPlank.Api.Composition.ServiceCollectionExtensions.DatabaseConnectionName}",
            ConnectionString);
        builder.UseSetting("OwnerBootstrap:Email", OwnerEmail);
        builder.UseSetting("OwnerBootstrap:InitialPassword", OwnerPassword);

        // The media file-store options are validated on host start. The acceptance scenarios never
        // upload a binary, but the host still requires a valid store root outside the web root and
        // positive size limits, so supply a throwaway temp directory and the production default
        // bounds (20 MB image / 200 MB video). The directory is created on demand by the file-store
        // adapter; the run writes no file through this path.
        builder.UseSetting("FileStore:RootPath", _mediaRootPath);
        builder.UseSetting("FileStore:ImageMaxBytes", "20971520");
        builder.UseSetting("FileStore:VideoMaxBytes", "209715200");
    }
}

/// <summary>
/// The xUnit collection definition sharing a single <see cref="AcceptanceApiFactory"/> (one
/// isolated database, one host) across the acceptance-scenario test classes. It is a distinct
/// collection from the schema-constraint group so the acceptance run gets its own isolated database
/// and never contends with the persistence tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AcceptanceApiTestGroup : ICollectionFixture<AcceptanceApiFactory>
{
    /// <summary>The collection name shared by every acceptance-scenario test class.</summary>
    public const string Name = "AcceptanceApi";
}
