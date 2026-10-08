using PenguinPlank.Application.IdentityAdministration;
using PenguinPlank.Infrastructure.IdentityAdministration;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Composition-root registrations for the one-time Owner bootstrap (requirement A2 §5.8).
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own file and extension method, additive to
/// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>, so the bootstrap wiring
/// stays localized and does not contend with the other identity/authorization registrations
/// being added in parallel. It is the single place that binds the operator-supplied
/// <see cref="OwnerBootstrapOptions"/> and maps the <see cref="IOwnerBootstrapper"/> boundary to
/// its Infrastructure implementation (coding-standards §2: registration happens only in the
/// composition root).
/// </para>
/// <para>
/// The initial Owner password is never hardcoded here: it is bound from configuration (a
/// deployment secret / user-secrets / environment variable surfaced through configuration) under
/// <see cref="OwnerBootstrapOptions.SectionName"/>, and the bootstrapper refuses a missing or
/// placeholder value rather than using a default.
/// </para>
/// </remarks>
public static class OwnerBootstrapServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Owner bootstrap options and service. Call this from the composition root
    /// alongside <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">
    /// Application configuration, read for the <see cref="OwnerBootstrapOptions.SectionName"/>
    /// section (operator-supplied Owner email and initial password).
    /// </param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddOwnerBootstrap(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        System.ArgumentNullException.ThrowIfNull(services);
        System.ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<OwnerBootstrapOptions>()
            .Bind(configuration.GetSection(OwnerBootstrapOptions.SectionName));

        // Scoped: the bootstrapper depends on UserManager/RoleManager, which are scoped over the
        // scoped DbContext. A singleton here would capture a scoped context (coding-standards §2;
        // requirement A2 §5.14). Registered only when a database is configured so the build/lint
        // path (no connection string, no DbContext) does not fail to resolve the dependency.
        if (!string.IsNullOrWhiteSpace(
                configuration.GetConnectionString(ServiceCollectionExtensions.DatabaseConnectionName)))
        {
            services.AddScoped<IOwnerBootstrapper, OwnerBootstrapper>();
        }

        return services;
    }
}
