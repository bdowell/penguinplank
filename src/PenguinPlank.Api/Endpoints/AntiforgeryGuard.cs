using Microsoft.AspNetCore.Antiforgery;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Validates the CSRF (antiforgery) token on a JSON-body mutating endpoint (requirement A5 §7.2).
/// </summary>
/// <remarks>
/// The <c>UseAntiforgery</c> middleware only auto-validates minimal API endpoints that bind form
/// data; a JSON-body <c>POST</c>/<c>PATCH</c>/<c>DELETE</c> must validate the token explicitly. This
/// small helper runs <see cref="IAntiforgery.ValidateRequestAsync"/> and, on failure, maps the
/// rejection to a consistent <c>400</c> ProblemDetails through the shared error surface so a
/// cross-site request that carries the session cookie but no valid token is refused with the same
/// contract as every other failure (coding-standards §3). It carries no business logic.
/// </remarks>
public static class AntiforgeryGuard
{
    /// <summary>
    /// Validates the antiforgery token for the current request.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <returns>
    /// A <c>null</c> result when the token is valid; otherwise a <c>400</c> ProblemDetails
    /// <see cref="IResult"/> describing the CSRF rejection.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">Thrown when a dependency is <see langword="null"/>.</exception>
    public static async Task<IResult?> ValidateAsync(HttpContext httpContext, IAntiforgery antiforgery)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);
        System.ArgumentNullException.ThrowIfNull(antiforgery);

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            // A missing or invalid CSRF token on a mutating request is expected caller input, not a
            // programming error; translate it to a 400 at this boundary rather than letting it fault.
            return CatalogResults.Problem(
                new BusinessError(ErrorCode.Validation, "A valid CSRF token is required on this request."),
                httpContext);
        }
    }
}
