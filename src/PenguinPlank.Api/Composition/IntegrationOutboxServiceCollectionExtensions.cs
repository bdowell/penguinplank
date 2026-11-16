using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Infrastructure.Integrations;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the transactional outbox boundary.
/// </summary>
/// <remarks>
/// The <see cref="EfOutboxStore"/> is <b>scoped</b> so it shares the scoped database context
/// (and therefore the transaction) of the business change it accompanies.
/// </remarks>
public static class IntegrationOutboxServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IOutboxStore"/> and its dependencies.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPenguinPlankIntegrationOutbox(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(System.TimeProvider.System);
        services.TryAddScoped<IOutboxStore, EfOutboxStore>();

        return services;
    }
}
