using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Infrastructure.Auditing;
using PenguinPlank.Infrastructure.IdentityAdministration;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// The single dependency-injection composition root for Penguin Plank.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place where Application boundary interfaces are bound to their
/// Infrastructure implementations (coding-standards §2, §3). Business code must obtain
/// its dependencies through constructor injection; it must never resolve services via
/// <see cref="System.IServiceProvider"/>, a service locator, a global container, or
/// ambient application state.
/// </para>
/// <para>
/// Later Phase A tasks register the catalog, media, identity/administration, integration
/// foundation, and cross-cutting implementations here (matching DI lifetimes correctly —
/// for example, a singleton must not capture a scoped <c>DbContext</c>).
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The configuration key holding the SQL Server connection string for the application
    /// database.
    /// </summary>
    public const string DatabaseConnectionName = "PenguinPlankDatabase";

    /// <summary>
    /// Registers Penguin Plank application and infrastructure services into the
    /// dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">
    /// Application configuration, read for the database connection string under
    /// <see cref="DatabaseConnectionName"/>. The secret is supplied by the deployment
    /// environment (user secrets, environment variable, or secret store) — never hardcoded.
    /// </param>
    /// <returns>
    /// The same <paramref name="services"/> instance, enabling fluent chaining.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        System.ArgumentNullException.ThrowIfNull(services);
        System.ArgumentNullException.ThrowIfNull(configuration);

        // Audit accessor/interceptor/sink are registered before persistence so the DbContext
        // registration can attach the scoped interceptor to its options (task 6.7).
        services.AddPenguinPlankAuditing();

        AddPersistence(services, configuration);
        AddIdentity(services);

        // Authorization policies (Owner-only / Staff-or-Owner) and the field-level allowlist
        // projectors are registered in a dedicated extension (task 4.3) to keep that wiring
        // isolated from persistence/identity and the Owner bootstrap registration.
        services.AddPenguinPlankAuthorization();

        // ETag / If-Match optimistic-concurrency boundary (task 6.5), isolated in its own
        // extension; registered scoped because it captures the scoped DbContext.
        services.AddPenguinPlankConcurrency();

        // Media IFileStore (local adapter outside the web root) + validated options (task 6.9),
        // isolated in its own extension; registered as singletons that capture no scoped context.
        services.AddPenguinPlankMediaStorage(configuration);

        // Idempotency pipeline + IIdempotencyStore (task 6.3), isolated in its own extension;
        // the store/pipeline are scoped (they capture the scoped DbContext).
        services.AddPenguinPlankIdempotency();

        // Catalog composition-root wiring that depends only on abstractions (task 8.2): the
        // identifier-generator and TimeProvider seams.
        services.AddPenguinPlankCatalog();

        // Catalog EF Core persistence (task 8.3): the narrow stores, the use cases, and the coarse
        // ICatalogReader/ICatalogWriter/ICareProfileStore boundaries bound to their adapters. All
        // scoped (they capture the scoped DbContext); registered after AddPenguinPlankCatalog so
        // the identifier-generator/TimeProvider seams it needs are already present.
        services.AddPenguinPlankCatalogPersistence();

        // Media metadata store + media use cases (task 9.2), isolated in its own extension. All
        // scoped (the EF metadata store captures the scoped DbContext); registered after
        // AddPenguinPlankMediaStorage so the IFileStore seam it composes is already present, and
        // after AddPenguinPlankCatalog so the identifier-generator/TimeProvider seams exist.
        services.AddPenguinPlankMedia();

        // Administration stores + use cases (task 9.3): business settings, staff-user
        // administration, and the audit-trail reader, isolated in their own extension. All scoped
        // (the EF/Identity stores capture scoped state); registered after AddPenguinPlankCatalog so
        // the TimeProvider seam the use cases need is already present.
        services.AddPenguinPlankAdministration();

        // Browser authentication (task 9.3): the IStaffAuthenticator abstraction bound to the Phase
        // A cookie implementation, application-cookie hardening, and the CSRF/antiforgery services.
        // Isolated in its own extension; the authenticator is scoped (it captures the scoped
        // Identity managers). Registered after AddIdentity so SignInManager/UserManager exist.
        services.AddPenguinPlankAuth();

        // This remains the single composition root: no business code resolves services at runtime.
        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core Identity for <b>staff accounts only</b> with the two Phase A roles
    /// (Owner and Staff) and the Penguin Plank database as the Identity store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This uses <c>AddRoles</c> on <c>AddIdentityCore</c>
    /// rather than the full <c>AddIdentity</c>/<c>AddDefaultIdentity</c> stack. That choice is
    /// deliberate: it registers the <c>UserManager</c>/<c>RoleManager</c>/stores needed to manage
    /// staff accounts and roles, but it brings <b>no public self-registration surface and no
    /// default Identity UI</b> (requirement A2 §5.9). Accounts are created only by the one-time
    /// Owner bootstrap (task 4.2) and owner-only administration endpoints (task 9.3). The browser
    /// authentication scheme (secure HttpOnly same-origin cookie + CSRF) is wired in task 9.3; this
    /// task is Identity configuration and schema only.
    /// </para>
    /// <para>
    /// The Identity stores are backed by the scoped <see cref="PenguinPlankDbContext"/>, so the
    /// Identity tables share the single database and migration chain. When no connection string is
    /// configured the context is not registered (see <see cref="AddPersistence"/>); Identity's EF
    /// stores are still registered here but are never resolved in that build/lint-only path. All
    /// Identity services are scoped or transient, so no singleton captures the scoped context
    /// (coding-standards §2; requirement A2 §5.14).
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to populate.</param>
    private static void AddIdentity(IServiceCollection services)
    {
        // ASP.NET Core Data Protection backs Identity's default token providers
        // (DataProtectorTokenProvider needs IDataProtectionProvider). AddIdentityCore does not
        // register it the way the full AddIdentity stack does, so register it explicitly here.
        services.AddDataProtection();

        // Register an authentication scheme so authorization (including the fallback
        // authenticated-user policy) has an IAuthenticationService to challenge with. Identity's
        // application cookie is the scheme the browser auth flow in task 9.3 builds on.
        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                // Phase A has no login UI yet (task 9.3 builds it). Until then, an
                // unauthenticated or unauthorized request must get a clean 401/403 API
                // response rather than a redirect to a nonexistent /Account/Login, which
                // would otherwise loop forever against the fallback authenticated-user policy.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services
            .AddIdentityCore<AppUser>(options =>
            {
                // Require a confirmed account before sign-in; staff accounts are provisioned by
                // an Owner, never self-registered.
                options.SignIn.RequireConfirmedAccount = false;

                // Usernames are the staff member's email; keep them unique.
                options.User.RequireUniqueEmail = true;

                // Password policy: a reasonable private-business baseline. The one-time Owner
                // bootstrap (task 4.2) refuses a baked-in default and requires an operator-supplied
                // password, so this policy guards every account including the first.
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                // Lockout: protect accounts from brute-force attempts.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PenguinPlankDbContext>()
            // SignInManager is not part of AddIdentityCore; the browser cookie sign-in flow
            // (task 9.3) needs it to validate credentials and establish the application-cookie
            // session. AddSignInManager registers it along with the user-claims principal factory.
            .AddSignInManager()
            .AddDefaultTokenProviders();
    }

    /// <summary>
    /// Registers the EF Core <see cref="PenguinPlankDbContext"/> against SQL Server when a
    /// connection string is configured.
    /// </summary>
    /// <remarks>
    /// The context is registered scoped (EF Core's default) so no singleton can capture it
    /// (coding-standards §2). Migrations are <b>not</b> applied here — they run as an
    /// explicit deployment step (task 3.5). When no connection string is present (for
    /// example in a build/lint environment with no database), registration is skipped so
    /// the host still starts; later tasks that require the database fail fast with a clear
    /// message rather than this foundation task hardcoding a fallback.
    /// </remarks>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The configuration to read the connection string from.</param>
    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(DatabaseConnectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        // The service-provider overload lets the scoped audit SaveChanges interceptor (task 6.7)
        // attach to the scoped context from the same scope, so no singleton captures a scoped
        // dependency (requirement A2 §5.14). The interceptor adds AuditEntry rows to this same
        // context inside the same SaveChanges transaction, so audit commits iff the mutation does.
        services.AddDbContext<PenguinPlankDbContext>((provider, options) =>
            options
                .UseSqlServer(connectionString)
                .AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>()));
    }
}
