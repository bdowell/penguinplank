using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Infrastructure.Integrations;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the connection and credential-reference stores (requirement R11 / 3.1-3.3).
/// </summary>
/// <remarks>
/// Both stores capture the scoped <c>PenguinPlankDbContext</c>, so they are scoped
/// (coding-standards §2).
/// </remarks>
public static class IntegrationConnectionServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IIntegrationConnectionStore"/> and <see cref="ICredentialStore"/>.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    public static IServiceCollection AddPenguinPlankIntegrationConnections(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IIntegrationConnectionStore, EfIntegrationConnectionStore>();
        services.AddScoped<ICredentialStore, EfCredentialStore>();

        return services;
    }
}
