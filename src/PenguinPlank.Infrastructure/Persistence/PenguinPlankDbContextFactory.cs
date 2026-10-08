using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PenguinPlank.Infrastructure.Persistence;

/// <summary>
/// A design-time factory that lets the EF Core migration tooling (<c>dotnet ef</c>) construct a
/// <see cref="PenguinPlankDbContext"/> without starting the application or reaching a real
/// database.
/// </summary>
/// <remarks>
/// <para>
/// The API composition root only registers the context when a real
/// <c>PenguinPlankDatabase</c> connection string is configured, so migration scaffolding — which
/// runs offline and inspects the model only — has no context to resolve from the host. EF Core
/// discovers this <see cref="IDesignTimeDbContextFactory{TContext}"/> implementation in the
/// Infrastructure assembly and uses it instead (design "migrations apply as an explicit
/// deployment step").
/// </para>
/// <para>
/// <b>The connection string below is design-time only and is not a secret.</b> It points at a
/// local SQL Server with no credentials and is never opened: <c>dotnet ef migrations add</c> and
/// <c>dotnet ef migrations script</c> build the model and emit SQL without connecting. It exists
/// solely so the <c>UseSqlServer</c> provider call
/// can select the SQL Server provider (whose type mappings the generated migration depends on).
/// Real deployments supply the production connection string through configuration and apply the
/// migration with <c>dotnet ef database update</c> or a generated idempotent SQL script.
/// </para>
/// </remarks>
public sealed class PenguinPlankDbContextFactory : IDesignTimeDbContextFactory<PenguinPlankDbContext>
{
    /// <summary>
    /// A credential-free, local-only connection string used purely to select the SQL Server
    /// provider during offline model building. It is never opened by migration scaffolding.
    /// </summary>
    private const string DesignTimeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=PenguinPlankDesignTime;Trusted_Connection=True;";

    /// <summary>
    /// Builds a <see cref="PenguinPlankDbContext"/> configured for the SQL Server provider so the
    /// migration tooling can scaffold and script migrations offline.
    /// </summary>
    /// <param name="args">Command-line arguments forwarded by the EF tooling (unused).</param>
    /// <returns>A context whose model matches the production SQL Server mapping.</returns>
    public PenguinPlankDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<PenguinPlankDbContext> optionsBuilder = new();

        optionsBuilder.UseSqlServer(
            DesignTimeConnectionString,
            sqlServerOptions => sqlServerOptions.MigrationsAssembly(
                typeof(PenguinPlankDbContext).Assembly.FullName));

        return new PenguinPlankDbContext(optionsBuilder.Options);
    }
}
