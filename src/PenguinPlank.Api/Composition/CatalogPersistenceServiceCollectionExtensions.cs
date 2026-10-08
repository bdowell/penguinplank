using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Infrastructure.Catalog.Persistence;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the EF Core catalog persistence adapters (task 8.3) and the catalog use cases they
/// compose into the dependency-injection container.
/// </summary>
/// <remarks>
/// <para>
/// This completes the catalog composition the <see cref="CatalogServiceCollectionExtensions"/>
/// seam deferred: the narrow persistence boundaries
/// (<see cref="ICatalogVariantStore"/>, <see cref="ICatalogPieceStore"/>,
/// <see cref="ICatalogWoodStore"/>, <see cref="IChannelListingStore"/>,
/// <see cref="ICareProfileVersionStore"/>) are bound to their EF implementations, the
/// single-responsibility use cases are registered, and the coarse Application boundaries
/// (<see cref="ICatalogReader"/>, <see cref="ICatalogWriter"/>, <see cref="ICareProfileStore"/>)
/// are bound to the Infrastructure adapters that compose them. This is the single composition
/// root; no business code resolves a service at runtime (coding-standards §2).
/// </para>
/// <para>
/// Every registration here is <b>scoped</b>: each store, use case, reader, and writer either
/// captures the scoped <c>PenguinPlankDbContext</c> directly or depends (transitively) on a
/// component that does, so a singleton registration would capture a scoped context and is forbidden
/// (coding-standards §2; requirement A2 §5.14). The stateless identifier-generator and
/// <c>TimeProvider</c> seams are registered as singletons by
/// <see cref="CatalogServiceCollectionExtensions.AddPenguinPlankCatalog"/>, which this extension
/// assumes has already run.
/// </para>
/// </remarks>
public static class CatalogPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Binds the catalog persistence stores, use cases, and coarse boundaries to their
    /// implementations.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankCatalogPersistence(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // Narrow EF persistence boundaries (task 8.2 contracts, task 8.3 implementations). Each
        // captures the scoped DbContext.
        services.AddScoped<ICatalogVariantStore, EfCatalogVariantStore>();
        services.AddScoped<ICatalogPieceStore, EfCatalogPieceStore>();
        services.AddScoped<ICatalogWoodStore, EfCatalogWoodStore>();
        services.AddScoped<IChannelListingStore, EfChannelListingStore>();
        services.AddScoped<ICareProfileVersionStore, EfCareProfileVersionStore>();

        // Catalog use cases (task 8.2). Plain orchestration classes resolved by constructor
        // injection; scoped because they depend on the scoped stores.
        services.AddScoped<CreateVariantUseCase>();
        services.AddScoped<CreatePieceUseCase>();
        services.AddScoped<SetWoodCompositionUseCase>();
        services.AddScoped<ArchiveVariantUseCase>();
        services.AddScoped<RecordChannelListingUseCase>();
        services.AddScoped<AddCareVersionUseCase>();

        // Coarse Application boundaries (task 8.1) bound to the Infrastructure adapters that
        // compose the stores/use cases.
        services.AddScoped<ICatalogReader, EfCatalogReader>();
        services.AddScoped<ICatalogWriter, CatalogWriter>();
        services.AddScoped<ICareProfileStore, CareProfileStore>();

        return services;
    }
}
