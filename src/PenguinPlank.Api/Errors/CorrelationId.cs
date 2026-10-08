namespace PenguinPlank.Api.Errors;

/// <summary>
/// Resolves the per-request correlation identifier attached to every ProblemDetails body and to
/// the matching structured log event (requirement A1 §4.8, A6 §8.5).
/// </summary>
/// <remarks>
/// A caller may propagate its own id through the <see cref="HeaderName"/> request header (useful
/// for correlating across a client and the API); otherwise the ASP.NET Core
/// <see cref="HttpContext.TraceIdentifier"/> — which is already unique per request — is used. The
/// resolved id is echoed back on the response header so a client can read it even when it did not
/// supply one.
/// </remarks>
public static class CorrelationId
{
    /// <summary>The request/response header carrying the correlation identifier.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// Returns the correlation id for the current request: the inbound <see cref="HeaderName"/>
    /// header value when present and non-whitespace, otherwise the request's
    /// <see cref="HttpContext.TraceIdentifier"/>.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A non-empty correlation identifier.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    public static string Resolve(HttpContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues supplied)
            && !string.IsNullOrWhiteSpace(supplied.ToString()))
        {
            return supplied.ToString();
        }

        return context.TraceIdentifier;
    }
}
