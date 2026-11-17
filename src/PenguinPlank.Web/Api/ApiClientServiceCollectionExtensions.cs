namespace PenguinPlank.Web.Api;

/// <summary>Composition-root registration for the typed API client.</summary>
public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the same-origin typed API clients. All services are scoped because
    /// <see cref="ApiRequestSender"/> caches the CSRF token for the user's session.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The application's own origin (the host base address).</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public static IServiceCollection AddPenguinPlankApiClient(this IServiceCollection services, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress });
        services.AddScoped<IIdempotencyKeyGenerator, GuidIdempotencyKeyGenerator>();
        services.AddScoped<ApiRequestSender>();
        services.AddScoped<IAuthApiClient, AuthApiClient>();
        services.AddScoped<ICatalogApiClient, CatalogApiClient>();
        return services;
    }
}
