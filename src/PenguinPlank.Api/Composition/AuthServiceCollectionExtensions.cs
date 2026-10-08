using PenguinPlank.Api.Authentication;
using PenguinPlank.Application.IdentityAdministration.Authentication;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the browser authentication abstraction, the application-cookie hardening, and the CSRF
/// (antiforgery) services (requirements A2 §5.9, A5 §7.1, §7.2, §7.3).
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own file and extension method, additive to
/// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>, so the authentication wiring
/// stays localized (coding-standards §2). It binds the narrow <see cref="IStaffAuthenticator"/>
/// abstraction to the Phase A cookie implementation; a future OIDC+PKCE implementation for the
/// mobile client swaps in here with no change to the login/logout callers (requirement A5 §7.3).
/// </para>
/// <para>
/// <b>Cookie hardening (requirement A5 §7.2).</b> The Identity application cookie is configured
/// HttpOnly, <c>SameSite=Strict</c>, and <c>SecurePolicy=Always</c>, so the browser session carries
/// no bearer token and is not sent cross-site. <b>CSRF.</b> The antiforgery service is registered
/// with a dedicated request-token header (<c>X-CSRF-TOKEN</c>) so the browser echoes the token the
/// <c>/auth/csrf</c> endpoint issues on every mutating request; the validation middleware is added
/// to the pipeline in <c>Program.cs</c>.
/// </para>
/// <para>
/// The <see cref="CookieStaffAuthenticator"/> captures the scoped <c>SignInManager</c>/
/// <c>UserManager</c> and <see cref="IHttpContextAccessor"/>, so it is registered <b>scoped</b> —
/// never a singleton capturing scoped state (coding-standards §2; requirement A2 §5.14).
/// </para>
/// </remarks>
public static class AuthServiceCollectionExtensions
{
    /// <summary>The request-token header the browser sends the antiforgery (CSRF) token in.</summary>
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";

    /// <summary>
    /// Registers the staff authentication abstraction, cookie hardening, and antiforgery services.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPenguinPlankAuth(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // The current-actor lookup reads the authenticated principal from the current request.
        services.AddHttpContextAccessor();

        // The narrow authentication abstraction (A5 §7.3). The Phase A implementation is the cookie
        // one; it captures scoped Identity managers and the request accessor, so it is scoped.
        services.AddScoped<IStaffAuthenticator, CookieStaffAuthenticator>();

        // Harden the Identity application cookie: HttpOnly (no script access), SameSite=Strict and
        // Secure (same-origin, TLS only) so the browser session is a secure HttpOnly same-origin
        // cookie with no bearer token in browser storage (requirement A5 §7.2).
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        // CSRF protection on mutations (requirement A5 §7.2): the browser echoes the request token
        // in this header; the antiforgery cookie is HttpOnly. The validation middleware is wired in
        // the pipeline (Program.cs) so a POST/PATCH/DELETE without a valid token is rejected.
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        return services;
    }
}
