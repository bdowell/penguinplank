using Microsoft.AspNetCore.Mvc;
using PenguinPlank.Api.Errors;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Translates a catalog use case's typed <see cref="Result"/> / <see cref="BusinessError"/>
/// outcome into the HTTP response an endpoint returns, reusing the single
/// <see cref="ErrorCodeHttpMapping"/> and <see cref="ProblemDetailsBuilder"/> error surface so the
/// contract a client observes matches what the unit tests assert (requirement A1 §4.8).
/// </summary>
/// <remarks>
/// Endpoints stay thin by delegating failure translation here (coding-standards §3): a business
/// failure becomes an RFC 7807 ProblemDetails carrying the mapped status, the stable error code,
/// and the request's correlation id. These helpers never contain business logic; they only map an
/// already-decided outcome onto the HTTP surface.
/// </remarks>
public static class CatalogResults
{
    /// <summary>
    /// Builds the ProblemDetails <see cref="IResult"/> for an expected business failure.
    /// </summary>
    /// <param name="error">The business failure carried by a failed result.</param>
    /// <param name="httpContext">The current request, used to resolve the correlation id.</param>
    /// <returns>A ProblemDetails result with the mapped status, stable code, and correlation id.</returns>
    public static IResult Problem(BusinessError error, HttpContext httpContext)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);

        string correlationId = CorrelationId.Resolve(httpContext);
        ProblemDetails problem = ProblemDetailsBuilder.FromBusinessError(error, correlationId);
        return Microsoft.AspNetCore.Http.Results.Problem(problem);
    }

    /// <summary>
    /// Builds a <c>404 Not Found</c> ProblemDetails for a read that matched no record. A missing
    /// record is not a business error code; it maps directly to the not-found status with the
    /// request's correlation id.
    /// </summary>
    /// <param name="httpContext">The current request, used to resolve the correlation id.</param>
    /// <param name="detail">A caller-safe description of what was not found.</param>
    /// <returns>A <c>404</c> ProblemDetails result.</returns>
    public static IResult NotFound(HttpContext httpContext, string detail)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);

        string correlationId = CorrelationId.Resolve(httpContext);
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = ProblemDetailsBuilder.TitleFor(StatusCodes.Status404NotFound),
            Detail = detail,
        };
        problem.Extensions[ProblemDetailsBuilder.CorrelationIdExtension] = correlationId;
        return Microsoft.AspNetCore.Http.Results.Problem(problem);
    }
}
