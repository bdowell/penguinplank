namespace PenguinPlank.Web.Shell;

/// <summary>Composition-root registration for the browser shell services.</summary>
public static class ShellServiceCollectionExtensions
{
    /// <summary>Registers the shell services. <see cref="AuthSession"/> is scoped to the user's session.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPenguinPlankShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<AuthSession>();
        return services;
    }
}
