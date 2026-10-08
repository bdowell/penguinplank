using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Persistence;

/// <summary>
/// Creates an isolated SQL Server database per test run and applies the <b>actual</b> EF Core
/// migration to it, then drops the database on teardown.
/// </summary>
/// <remarks>
/// <para>
/// coding-standards §7 requires relational-constraint integration tests to run against a real
/// SQL Server using the actual migrations — EF InMemory is not evidence of relational
/// correctness. The fixture therefore connects to the local SQL Server Express instance with
/// Windows authentication, creates a uniquely named database (so parallel/repeated runs never
/// collide or share state), and calls <c>Database.MigrateAsync()</c>
/// so the schema is built exactly by the committed migration rather than
/// by <c>EnsureCreated</c>.
/// </para>
/// <para>
/// The fixture keeps itself small and explicit (coding-standards §7): it seeds nothing. Each
/// test arranges only the rows it needs through a fresh <see cref="CreateContext"/> instance.
/// <see cref="DisposeAsync"/> drops the isolated database so no artifact survives the run.
/// </para>
/// </remarks>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private const string ServerConnectionString =
        "Server=localhost\\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True";

    private readonly string _databaseName =
        $"PenguinPlank_IntegrationTests_{Guid.NewGuid():N}";

    /// <summary>The connection string targeting the isolated per-run database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Creates the isolated database and applies the committed migration to it. Any failure here
    /// (for example, SQL Server not reachable) surfaces as a test-collection failure with the
    /// real exception rather than a silent fallback to a non-relational provider.
    /// </summary>
    public async Task InitializeAsync()
    {
        SqlConnectionStringBuilder builder = new(ServerConnectionString)
        {
            InitialCatalog = _databaseName,
        };
        ConnectionString = builder.ConnectionString;

        await CreateDatabaseAsync();

        await using PenguinPlankDbContext context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>Drops the isolated database, removing every artifact created during the run.</summary>
    public async Task DisposeAsync()
    {
        await using SqlConnection connection = new(ServerConnectionString);
        await connection.OpenAsync();

        // Force the database single-user to evict any pooled connections, then drop it.
        string sql =
            $"IF DB_ID(N'{_databaseName}') IS NOT NULL " +
            $"BEGIN " +
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{_databaseName}]; " +
            $"END";

        SqlConnection.ClearAllPools();

        await using SqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Builds a context bound to the isolated database. Tests dispose each context they create
    /// so a stale change tracker never leaks between arrange/act/assert phases.
    /// </summary>
    public PenguinPlankDbContext CreateContext()
    {
        DbContextOptions<PenguinPlankDbContext> options =
            new DbContextOptionsBuilder<PenguinPlankDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

        return new PenguinPlankDbContext(options);
    }

    private async Task CreateDatabaseAsync()
    {
        await using SqlConnection connection = new(ServerConnectionString);
        await connection.OpenAsync();

        await using SqlCommand command =
            new($"CREATE DATABASE [{_databaseName}]", connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// The xUnit collection definition that shares a single <see cref="SqlServerDatabaseFixture"/>
/// (one isolated database, one migration apply) across the schema-constraint tests, keeping the
/// run fast while each test still arranges its own rows.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerDatabaseTestGroup : ICollectionFixture<SqlServerDatabaseFixture>
{
    /// <summary>The collection name shared by every schema-constraint test class.</summary>
    public const string Name = "SqlServerDatabase";
}
