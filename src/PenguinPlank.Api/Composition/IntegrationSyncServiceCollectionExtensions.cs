using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Infrastructure.Integrations;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the sync checkpoint, sync history/exception queue, and pause/resume boundaries
/// (requirements 3.9, 3.11, 3.12, 3.13). The stores capture the scoped database context, so they
/// are scoped.
/// </summary>
public static class IntegrationSyncServiceCollectionExtensions
{
    /// <summary>Registers the sync stores and the sync history service.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPenguinPlankIntegrationSync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ISyncCheckpointStore, EfSyncCheckpointStore>();
        services.TryAddScoped<ISyncHistoryStore, EfSyncHistoryStore>();
        services.TryAddScoped<IConnectionPauseStore, EfConnectionPauseStore>();
        services.TryAddScoped<SyncHistoryService>();
        return services;
    }
}
