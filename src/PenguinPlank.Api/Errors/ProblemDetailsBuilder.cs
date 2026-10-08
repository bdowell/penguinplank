using Microsoft.AspNetCore.Mvc;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Errors;

/// <summary>
/// Builds RFC 7807 <see cref="ProblemDetails"/> bodies for the two kinds of failure the API
/// surface produces: an <em>expected</em> business failure carried by a <see cref="Result"/> /
/// <see cref="BusinessError"/>, and an <em>unexpected</em> exception (requirement A1 §4.8,
/// A6 §8.5).
/// </summary>
/// <remarks>
/// <para>
/// Every method here is a <b>pure</b> function of ordinary inputs — a business error (or nothing),
/// a correlation identifier, and an optional caller-safe title. None of them read
/// <c>HttpContext</c>, a clock, configuration, or any ambient state, so the exact shape of an error
/// body can be asserted in a unit test without starting the application (coding-standards §1).
/// </para>
/// <para>
/// Two invariants protect clients:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     Every body carries the stable machine-readable <c>errorCode</c> extension member and the
///     <c>correlationId</c> extension member, so a client always has a stable code to branch on
///     and an identifier to quote in a support request.
///     </description>
///   </item>
///   <item>
///     <description>
///     An unexpected failure's body contains <b>no</b> internal detail — no exception message,
///     stack trace, SQL text, or secret. The caller receives only a generic description plus the
///     correlation id; the real detail is logged server-side against that same id
///     (requirement A6 §8.5).
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class ProblemDetailsBuilder
{
    /// <summary>The ProblemDetails extension member holding the stable machine-readable code.</summary>
    public const string ErrorCodeExtension = "errorCode";

    /// <summary>The ProblemDetails extension member holding the per-request correlation id.</summary>
    public const string CorrelationIdExtension = "correlationId";

    /// <summary>
    /// Builds a <see cref="ProblemDetails"/> for an expected business failure. The HTTP status and
    /// stable code come from <see cref="ErrorCodeHttpMapping"/>; the caller-safe
    /// <see cref="BusinessError.Message"/> becomes the detail.
    /// </summary>
    /// <param name="error">The expected business failure to translate.</param>
    /// <param name="correlationId">
    /// The per-request correlation identifier attached to the body and to the matching log event.
    /// </param>
    /// <returns>A ProblemDetails carrying the mapped status, stable code, and correlation id.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="correlationId"/> is null, empty, or whitespace.
    /// </exception>
    public static ProblemDetails FromBusinessError(BusinessError error, string correlationId)
    {
        RequireCorrelationId(correlationId);

        ErrorCodeHttpMapping mapping = ErrorCodeHttpMapping.Map(error.Code);

        // The BusinessError message is caller-safe by construction (coding-standards §6: it never
        // embeds credentials, SQL text, or secrets), so it is safe to surface as the detail.
        var problem = new ProblemDetails
        {
            Status = mapping.StatusCode,
            Title = TitleFor(mapping.StatusCode),
            Detail = error.Message,
        };

        problem.Extensions[ErrorCodeExtension] = mapping.Code;
        problem.Extensions[CorrelationIdExtension] = correlationId;
        return problem;
    }

    /// <summary>
    /// Builds the generic <c>500</c> <see cref="ProblemDetails"/> for an unexpected failure. The
    /// body deliberately carries <b>no</b> internal detail — only a generic description, the stable
    /// <see cref="ErrorCodeHttpMapping.UnexpectedCode"/> code, and the correlation id
    /// (requirement A6 §8.5).
    /// </summary>
    /// <param name="correlationId">
    /// The per-request correlation identifier the client can quote; the real failure detail is
    /// logged server-side against the same id.
    /// </param>
    /// <returns>A generic internal-error ProblemDetails with no leaked detail.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="correlationId"/> is null, empty, or whitespace.
    /// </exception>
    public static ProblemDetails Unexpected(string correlationId)
    {
        RequireCorrelationId(correlationId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = TitleFor(StatusCodes.Status500InternalServerError),
            Detail = "An unexpected error occurred. Quote the correlation id when reporting it.",
        };

        problem.Extensions[ErrorCodeExtension] = ErrorCodeHttpMapping.UnexpectedCode;
        problem.Extensions[CorrelationIdExtension] = correlationId;
        return problem;
    }

    /// <summary>
    /// A short, caller-safe title for a status code. Titles describe the category of failure only
    /// and never embed request-specific or sensitive detail.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the title describes.</param>
    /// <returns>A generic reason phrase for <paramref name="statusCode"/>.</returns>
    public static string TitleFor(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Invalid request",
            StatusCodes.Status401Unauthorized => "Authentication required",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status412PreconditionFailed => "Precondition failed",
            StatusCodes.Status413PayloadTooLarge => "Payload too large",
            StatusCodes.Status429TooManyRequests => "Too many requests",
            StatusCodes.Status500InternalServerError => "Internal server error",
            _ => "Error",
        };
    }

    private static void RequireCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new System.ArgumentException(
                "A correlation id is required on every ProblemDetails.",
                nameof(correlationId));
        }
    }
}
