using PenguinPlank.Api.Errors;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Composition-root registrations for the RFC 7807 ProblemDetails error surface
/// (requirement A1 §4.8, A6 §8.5).
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own file and extension method, additive to
/// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>, so the error-handling wiring
/// stays localized and does not contend with the persistence/identity/authorization/bootstrap
/// registrations being added by parallel foundation tasks. It is still part of the one composition
/// root (coding-standards §2).
/// </para>
/// <para>
/// It registers two things:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///     ASP.NET Core's ProblemDetails service (<c>AddProblemDetails</c>) with a shared customization
///     that stamps the per-request <c>correlationId</c> onto <b>every</b> ProblemDetails — including
///     framework-produced ones such as a <c>404</c>, a <c>401</c> authentication challenge, or a
///     <c>429</c> from rate limiting — and echoes it on the response header. The customization also
///     fills in a stable <c>errorCode</c> for those framework-produced statuses that reach the
///     response without one, so the Property 24 invariant (every error body carries a stable code
///     and a correlation id) holds for the whole surface, not only business failures.
///     </description>
///   </item>
///   <item>
///     <description>
///     The <see cref="UnhandledExceptionHandler"/> that converts an unexpected exception into a
///     generic <c>500</c> with no leaked detail.
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class ProblemDetailsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ProblemDetails service, its correlation-id/stable-code customization, and the
    /// unhandled-exception handler. Call this from the composition root alongside
    /// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankProblemDetails(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = static context =>
            {
                string correlationId = CorrelationId.Resolve(context.HttpContext);

                // Attach the correlation id to every ProblemDetails and echo it on the response so
                // a client can quote it even when it did not supply one (requirement A1 §4.8).
                context.ProblemDetails.Extensions[ProblemDetailsBuilder.CorrelationIdExtension] =
                    correlationId;
                context.HttpContext.Response.Headers[CorrelationId.HeaderName] = correlationId;

                // Guarantee a stable machine-readable code on framework-produced error bodies
                // (for example a bare 404/401/429 that did not come from a BusinessError). Business
                // failures set this via ProblemDetailsBuilder before reaching here, so only fill it
                // when it is absent.
                if (!context.ProblemDetails.Extensions.ContainsKey(ProblemDetailsBuilder.ErrorCodeExtension))
                {
                    int status = context.ProblemDetails.Status
                        ?? context.HttpContext.Response.StatusCode;
                    context.ProblemDetails.Extensions[ProblemDetailsBuilder.ErrorCodeExtension] =
                        StableCodeForStatus(status);

                    if (string.IsNullOrEmpty(context.ProblemDetails.Title))
                    {
                        context.ProblemDetails.Title = ProblemDetailsBuilder.TitleFor(status);
                    }
                }
            });

        services.AddExceptionHandler<UnhandledExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Returns a stable machine-readable code for a framework-produced HTTP error status that did
    /// not originate from a <c>BusinessError</c> (so it has no mapped <c>ErrorCode</c>).
    /// </summary>
    /// <param name="statusCode">The response status code.</param>
    /// <returns>A stable, caller-safe code string for the status category.</returns>
    private static string StableCodeForStatus(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "BadRequest",
            StatusCodes.Status401Unauthorized => "Unauthenticated",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "NotFound",
            StatusCodes.Status405MethodNotAllowed => "MethodNotAllowed",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status412PreconditionFailed => "PreconditionFailed",
            StatusCodes.Status413PayloadTooLarge => "PayloadTooLarge",
            StatusCodes.Status415UnsupportedMediaType => "UnsupportedMediaType",
            StatusCodes.Status429TooManyRequests => "TooManyRequests",
            >= 500 => ErrorCodeHttpMapping.UnexpectedCode,
            _ => "Error",
        };
    }
}
