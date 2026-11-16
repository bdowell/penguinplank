using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Infrastructure.Integrations;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the external-to-internal entity mapping boundary.
/// </summary>
/// <remarks>
/// The <see cref="EfEntityMappingStore"/> is <b>scoped</b> because it captures the scoped
/// database context.
/// </remarks>
public static class IntegrationEntityMappingServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IEntityMappingStore"/> and its dependencies.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPenguinPlankIntegrationEntityMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IEntityMappingStore, EfEntityMappingStore>();

        return services;
    }
}
