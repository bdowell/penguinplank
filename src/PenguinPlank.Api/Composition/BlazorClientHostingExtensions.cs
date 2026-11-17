namespace PenguinPlank.Api.Composition;

/// <summary>
/// Hosts the Blazor WebAssembly client from the API so the browser app and the API share one
/// origin (the same-origin cookie and CSRF design, requirement A5 §7.2).
/// </summary>
public static class BlazorClientHostingExtensions
{
    /// <summary>
    /// The fallback route template. <c>nonfile</c> (the framework default) leaves requests for
    /// missing files such as <c>/app.js</c> as 404, and <c>notapi</c> rejects any path under
    /// <c>/api</c>, so an unknown API path is never answered with <c>index.html</c>.
    /// </summary>
    public const string ClientFallbackPattern =
        "{*path:nonfile:" + NotApiPathRouteConstraint.ConstraintName + "}";

    /// <summary>The client entry document served for client-side routes.</summary>
    public const string ClientEntryFile = "index.html";

    /// <summary>Registers the <c>notapi</c> route constraint used by the client fallback route.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddBlazorClientHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<RouteOptions>(options =>
            options.ConstraintMap[NotApiPathRouteConstraint.ConstraintName] = typeof(NotApiPathRouteConstraint));
        return services;
    }

    /// <summary>
    /// Serves the Blazor framework files (<c>_framework</c>) and other static web assets. Static
    /// file middleware runs before authorization, so the client shell loads before sign-in.
    /// </summary>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The same pipeline for chaining.</returns>
    public static IApplicationBuilder UseBlazorClientFiles(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        return app;
    }

    /// <summary>
    /// Maps non-<c>/api</c> routes to <c>index.html</c> so client-side routing works on refresh.
    /// The endpoint is anonymous because the fallback authorization policy would otherwise block
    /// the shell the user needs in order to reach the sign-in page. It exposes no business data.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The fallback endpoint convention builder.</returns>
    public static IEndpointConventionBuilder MapBlazorClientFallback(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints
            .MapFallbackToFile(ClientFallbackPattern, ClientEntryFile)
            .AllowAnonymous();
    }
}
