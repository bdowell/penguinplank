using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog.UseCases;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the catalog composition-root wiring that depends only on abstractions.
/// </summary>
/// <remarks>
/// <para>
/// Phase A task 8.2 implements the catalog <b>use cases</b> that orchestrate the pure Domain
/// policies. The use cases depend on narrow persistence boundaries
/// (<c>ICatalogVariantStore</c>, <c>ICatalogPieceStore</c>, <c>ICatalogWoodStore</c>,
/// <c>IChannelListingStore</c>, <c>ICareProfileVersionStore</c>) whose EF Core implementations are
/// supplied by task 8.3. To keep the composition root free of a half-built graph, this extension
/// registers only what has an implementation today: the
/// <see cref="IIdentifierGenerator"/> seam, whose default <see cref="GuidIdentifierGenerator"/>
/// lives in the Application layer and captures no scoped state. The use-case classes and their EF
/// store bindings are registered by task 8.3/9.3 once the stores exist, keeping each registration
/// at the single composition root (coding-standards §2).
/// </para>
/// <para>
/// <see cref="Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}(IServiceCollection)"/>
/// is used so this coexists with any future registration of the same seam; the first registration
/// wins. The generator is a singleton because it is stateless and holds no scoped dependency
/// (A2 §5.14).
/// </para>
/// </remarks>
public static class CatalogServiceCollectionExtensions
{
    /// <summary>
    /// Registers the catalog abstractions that have an implementation in Phase A today.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPenguinPlankCatalog(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // Shared clock. TryAdd so this coexists with the other foundation extensions that register
        // the same system TimeProvider; the first registration wins and all share one instance.
        services.TryAddSingleton(System.TimeProvider.System);

        // The identifier generator seam used by the catalog create use cases. Registered now
        // because its default implementation lives in Application and needs no EF store. The use
        // cases themselves and their EF-backed stores are registered in task 8.3/9.3.
        services.TryAddSingleton<IIdentifierGenerator, GuidIdentifierGenerator>();

        return services;
    }

    /// <summary>
    /// Documents the catalog use-case types registered once their EF-backed persistence stores
    /// exist (task 8.3/9.3). Referencing the type names here keeps a compile-time anchor to the
    /// use cases from the composition root without binding a half-built graph today.
    /// </summary>
    /// <remarks>
    /// The use cases are plain orchestration classes resolved by constructor injection:
    /// <see cref="CreateVariantUseCase"/>, <see cref="ArchiveVariantUseCase"/>,
    /// <see cref="CreatePieceUseCase"/>, <see cref="SetWoodCompositionUseCase"/>,
    /// <see cref="RecordChannelListingUseCase"/>, and <see cref="AddCareVersionUseCase"/>.
    /// </remarks>
    internal static Type[] DeferredUseCaseTypes =>
    [
        typeof(CreateVariantUseCase),
        typeof(ArchiveVariantUseCase),
        typeof(CreatePieceUseCase),
        typeof(SetWoodCompositionUseCase),
        typeof(RecordChannelListingUseCase),
        typeof(AddCareVersionUseCase),
    ];
}
