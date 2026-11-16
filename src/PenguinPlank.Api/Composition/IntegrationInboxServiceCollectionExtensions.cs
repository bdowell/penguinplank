using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Infrastructure.Integrations;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the durable inbound-notification inbox boundary.
/// </summary>
/// <remarks>
/// The <see cref="EfInboxStore"/> is <b>scoped</b> because it captures the scoped database
/// context. The shared <see cref="System.TimeProvider"/> is added only if not already registered.
/// </remarks>
public static class IntegrationInboxServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IInboxStore"/> and its dependencies.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPenguinPlankIntegrationInbox(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(System.TimeProvider.System);
        services.TryAddScoped<IInboxStore, EfInboxStore>();

        return services;
    }
}
