using Microsoft.AspNetCore.Antiforgery;
using PenguinPlank.Application.IdentityAdministration.Authentication;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Contracts.Authentication;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps the thin authentication and session endpoints onto the <see cref="IStaffAuthenticator"/>
/// abstraction and the antiforgery service: login, logout, the current-actor lookup, and the CSRF
/// token (requirements A2 §5.9, A5 §7.1, §7.2, §7.3).
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint is deliberately thin (coding-standards §3): it hands the authentication
/// abstraction ordinary inputs and maps the typed <see cref="Result"/> / view to an HTTP response.
/// Login and logout depend only on <see cref="IStaffAuthenticator"/>, so the Phase A cookie
/// implementation can be replaced by an OIDC+PKCE one for a future mobile client without changing
/// these callers (requirement A5 §7.3). There is no self-registration surface here — the only
/// account-creation path is the Owner-only <c>/users</c> endpoint (requirement A2 §5.9).
/// </para>
/// <para>
/// <b>Cookie + CSRF.</b> A successful login establishes a secure, HttpOnly, same-origin cookie; no
/// bearer token is returned to the browser (requirement A5 §7.2). The <c>GET /auth/csrf</c>
/// endpoint issues an antiforgery request token (and sets the matching antiforgery cookie); the
/// browser echoes the request token in the configured header on every mutating request. Login
/// itself validates the antiforgery token to prevent login CSRF, so a client fetches the token
/// first. Logout and the current-actor lookup round out the session surface.
/// </para>
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>The route prefix the authentication endpoints share.</summary>
    public const string RoutePrefix = "/api/v1/auth";

    /// <summary>
    /// Maps the authentication endpoints under <see cref="RoutePrefix"/> onto the route builder.
    /// </summary>
    /// <param name="routes">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="routes"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="routes"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        System.ArgumentNullException.ThrowIfNull(routes);

        MapCsrfToken(routes);
        MapLogin(routes);
        MapLogout(routes);
        MapCurrentActor(routes);

        return routes;
    }

    private static void MapCsrfToken(IEndpointRouteBuilder routes)
    {
        // A safe GET: it issues the antiforgery request token and sets the antiforgery cookie so the
        // browser can send the token back on subsequent mutations. Anonymous so the login page can
        // obtain a token before authenticating.
        routes.MapGet($"{RoutePrefix}/csrf", (HttpContext httpContext, IAntiforgery antiforgery) =>
        {
            AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(httpContext);
            var response = new AntiforgeryTokenResponse
            {
                RequestToken = tokens.RequestToken ?? string.Empty,
                HeaderName = tokens.HeaderName ?? string.Empty,
            };
            return Microsoft.AspNetCore.Http.Results.Ok(response);
        })
        .AllowAnonymous();
    }

    private static void MapLogin(IEndpointRouteBuilder routes)
    {
        // Login is anonymous (no session exists yet) but still validates the antiforgery token to
        // prevent login CSRF, so a client fetches /auth/csrf first and sends the token here.
        routes.MapPost($"{RoutePrefix}/login", async (HttpContext httpContext, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, IStaffAuthenticator authenticator, LoginContract body) =>
        {
            IResult? csrfFailure = await AntiforgeryGuard.ValidateAsync(httpContext, antiforgery).ConfigureAwait(false);
            if (csrfFailure is not null)
            {
                return csrfFailure;
            }

            if (body is null)
            {
                return CatalogResults.Problem(new BusinessError(ErrorCode.Validation, "A request body is required."), httpContext);
            }

            var request = new SignInRequest(body.Email, body.Password);
            Result<AuthenticatedActor> result = await authenticator
                .SignInAsync(request, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                // Credentials are not accepted: surface a 401 rather than a generic validation 400,
                // with the uniform message that does not disclose which accounts exist.
                return Microsoft.AspNetCore.Http.Results.Problem(
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Microsoft.AspNetCore.Http.Results.Ok(ToResponse(result.Value));
        })
        .AllowAnonymous();
    }

    private static void MapLogout(IEndpointRouteBuilder routes)
    {
        // Logout clears the session cookie. It requires an authenticated session (the fallback
        // policy applies) and antiforgery validation as a mutating request.
        routes.MapPost($"{RoutePrefix}/logout", async (HttpContext httpContext, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, IStaffAuthenticator authenticator) =>
        {
            IResult? csrfFailure = await AntiforgeryGuard.ValidateAsync(httpContext, antiforgery).ConfigureAwait(false);
            if (csrfFailure is not null)
            {
                return csrfFailure;
            }

            await authenticator.SignOutAsync(httpContext.RequestAborted).ConfigureAwait(false);
            return Microsoft.AspNetCore.Http.Results.NoContent();
        });
    }

    private static void MapCurrentActor(IEndpointRouteBuilder routes)
    {
        // "Who am I" for the browser shell. Requires an authenticated session (the fallback policy
        // applies); returns the role-safe current actor.
        routes.MapGet($"{RoutePrefix}/me", (HttpContext httpContext, IStaffAuthenticator authenticator) =>
        {
            AuthenticatedActor? actor = authenticator.GetCurrentActor();
            if (actor is null)
            {
                return Microsoft.AspNetCore.Http.Results.Problem(statusCode: StatusCodes.Status401Unauthorized);
            }

            return Microsoft.AspNetCore.Http.Results.Ok(ToResponse(actor));
        });
    }

    private static CurrentActorResponse ToResponse(AuthenticatedActor actor) => new()
    {
        UserId = actor.UserId,
        Email = actor.Email,
        Role = RoleNames.FromRole(actor.Role),
    };
}
