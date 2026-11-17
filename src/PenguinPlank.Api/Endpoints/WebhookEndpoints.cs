using Microsoft.AspNetCore.Mvc;
using PenguinPlank.Api.Errors;
using PenguinPlank.Application.Integrations.Webhooks;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps the provider callback surface <c>POST /api/integrations/webhooks/{provider}</c>
/// (requirements 3.6, 3.7, 9.1 to 9.4).
/// </summary>
/// <remarks>
/// The endpoint is anonymous because providers cannot hold a staff cookie; its only gate is the
/// provider signature, checked by <see cref="ReceiveWebhookUseCase"/> before anything is recorded
/// or acknowledged. It is a write-only surface: there are no read routes, so it exposes no business
/// data. The body is read with a hard size bound before any processing.
/// </remarks>
public static class WebhookEndpoints
{
    /// <summary>The route prefix for provider callbacks.</summary>
    public const string RoutePrefix = "/api/integrations/webhooks";

    /// <summary>Maps the webhook endpoint onto the route builder.</summary>
    /// <param name="routes">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="routes"/> instance.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="routes"/> is null.</exception>
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        System.ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost(
            $"{RoutePrefix}/{{provider}}",
            async (string provider, HttpContext httpContext, ReceiveWebhookUseCase useCase, WebhookIntakeOptions options) =>
            {
                byte[]? body = await ReadBoundedBodyAsync(httpContext.Request, options.MaxPayloadBytes, httpContext.RequestAborted)
                    .ConfigureAwait(false);
                if (body is null)
                {
                    return ToResult(WebhookIntakeOutcome.PayloadTooLarge, httpContext);
                }

                var headers = httpContext.Request.Headers.ToDictionary(
                    header => header.Key,
                    header => header.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);
                var callback = new WebhookCallback(provider, body, headers);

                WebhookIntakeOutcome outcome = await useCase
                    .ReceiveAsync(callback, httpContext.RequestAborted)
                    .ConfigureAwait(false);
                return ToResult(outcome, httpContext);
            })
            .AllowAnonymous()
            .DisableAntiforgery();

        return routes;
    }

    /// <summary>Maps an intake outcome to its HTTP response.</summary>
    /// <param name="outcome">The intake outcome.</param>
    /// <param name="httpContext">The request, used for the correlation id.</param>
    /// <returns>A bodyless 202 for a durable delivery; otherwise a ProblemDetails.</returns>
    public static IResult ToResult(WebhookIntakeOutcome outcome, HttpContext httpContext)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);

        return outcome switch
        {
            WebhookIntakeOutcome.Recorded or WebhookIntakeOutcome.Duplicate
                => Microsoft.AspNetCore.Http.Results.Accepted(),
            WebhookIntakeOutcome.PayloadTooLarge
                => Fail(StatusCodes.Status413PayloadTooLarge, "The callback body exceeds the allowed size.", httpContext),
            WebhookIntakeOutcome.UnknownProvider or WebhookIntakeOutcome.UnknownConnection
                => Fail(StatusCodes.Status404NotFound, "No such provider endpoint.", httpContext),
            _ => Fail(StatusCodes.Status401Unauthorized, "The callback could not be authenticated.", httpContext),
        };
    }

    /// <summary>
    /// Reads the request body up to a limit.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="maxBytes">The inclusive maximum body size.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The body, or <see langword="null"/> when it exceeds <paramref name="maxBytes"/>.</returns>
    public static async Task<byte[]?> ReadBoundedBodyAsync(
        HttpRequest request,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(request);

        if (request.ContentLength is long declared && declared > maxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static IResult Fail(int status, string detail, HttpContext httpContext)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = ProblemDetailsBuilder.TitleFor(status),
            Detail = detail,
        };
        problem.Extensions[ProblemDetailsBuilder.CorrelationIdExtension] = CorrelationId.Resolve(httpContext);
        return Microsoft.AspNetCore.Http.Results.Problem(problem);
    }
}
