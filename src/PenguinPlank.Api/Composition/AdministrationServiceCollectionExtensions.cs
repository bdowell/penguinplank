using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Infrastructure.Auditing;
using PenguinPlank.Infrastructure.IdentityAdministration;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the administration persistence stores and use cases — business settings, staff-user
/// administration, and the audit-trail reader (requirement A2 §5.3, §5.9, §5.10, §5.12) — into the
/// dependency-injection container.
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own file and extension method, additive to
/// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>, so the administration wiring
/// stays localized (coding-standards §2). It binds the narrow Application boundaries
/// (<see cref="IBusinessSettingsStore"/>, <see cref="IStaffUserStore"/>, <see cref="IAuditReader"/>)
/// to their Infrastructure implementations and registers the thin orchestration use cases the
/// Owner-only administration endpoints map onto.
/// </para>
/// <para>
/// Every registration here is <b>scoped</b>: the EF stores capture the scoped
/// <c>PenguinPlankDbContext</c>, the Identity staff-user store captures the scoped
/// <c>UserManager</c> and context, and each use case depends (transitively) on one of them, so a
/// singleton registration would capture scoped state and is forbidden (coding-standards §2;
/// requirement A2 §5.14). The stateless <c>TimeProvider</c> seam the use cases compose is
/// registered as a singleton by <see cref="CatalogServiceCollectionExtensions.AddPenguinPlankCatalog"/>,
/// which this extension assumes has already run.
/// </para>
/// </remarks>
public static class AdministrationServiceCollectionExtensions
{
    /// <summary>
    /// Binds the administration stores and registers the administration use cases.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPenguinPlankAdministration(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // Narrow persistence boundaries; each captures the scoped DbContext (and, for the staff-user
        // store, the scoped UserManager).
        services.AddScoped<IBusinessSettingsStore, EfBusinessSettingsStore>();
        services.AddScoped<IStaffUserStore, IdentityStaffUserStore>();
        services.AddScoped<IAuditReader, EfAuditReader>();

        // Administration use cases: plain orchestration classes resolved by constructor injection;
        // scoped because they depend on the scoped stores.
        services.AddScoped<GetBusinessSettingsUseCase>();
        services.AddScoped<UpdateBusinessSettingsUseCase>();
        services.AddScoped<ListStaffUsersUseCase>();
        services.AddScoped<CreateStaffUserUseCase>();
        services.AddScoped<ListAuditEntriesUseCase>();

        return services;
    }
}
