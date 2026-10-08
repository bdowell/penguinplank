using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PenguinPlank.Api.Errors;

/// <summary>
/// The last-resort <see cref="IExceptionHandler"/> that converts any <em>unexpected</em> exception
/// escaping the pipeline into a generic <c>500</c> <see cref="ProblemDetails"/> response, leaking
/// no internal detail to the client (requirement A1 §4.8, A6 §8.5).
/// </summary>
/// <remarks>
/// <para>
/// Expected business failures do not reach here: they are modeled as <c>Result</c> /
/// <c>BusinessError</c> and translated to ProblemDetails at the endpoint boundary by later tasks
/// using <see cref="ProblemDetailsBuilder.FromBusinessError"/>. This handler exists for the
/// <em>unexpected</em> path — a bug, a transient infrastructure fault, or any other exception — so
/// a stack trace, SQL message, or secret is never serialized to the client. The response body is
/// built by <see cref="ProblemDetailsBuilder.Unexpected"/> and carries only a generic description,
/// the stable <c>Unexpected</c> error code, and the correlation id.
/// </para>
/// <para>
/// The full exception <b>is</b> captured — but only server-side, as a single structured log event
/// keyed by the same correlation id the client receives. The log records the correlation id and
/// the exception itself (type, message, stack trace) for the operator; it does <b>not</b> record
/// request bodies, headers, credentials, or other PII (requirement A6 §8.5). The exception is
/// logged once here, at this defined boundary, rather than repeatedly up the stack.
/// </para>
/// </remarks>
public sealed partial class UnhandledExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<UnhandledExceptionHandler> _logger;

    /// <summary>
    /// Initializes the handler with the ProblemDetails writer and logger it needs. The constructor
    /// only stores its dependencies (coding-standards §2).
    /// </summary>
    /// <param name="problemDetailsService">
    /// The framework service that writes a <see cref="ProblemDetails"/> to the response using the
    /// configured content negotiation and customization.
    /// </param>
    /// <param name="logger">The structured logger for the single server-side failure event.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when any dependency is <see langword="null"/>.
    /// </exception>
    public UnhandledExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<UnhandledExceptionHandler> logger)
    {
        System.ArgumentNullException.ThrowIfNull(problemDetailsService);
        System.ArgumentNullException.ThrowIfNull(logger);

        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);
        System.ArgumentNullException.ThrowIfNull(exception);

        string correlationId = CorrelationId.Resolve(httpContext);

        // Log once, server-side, keyed by the correlation id the client receives. The exception
        // object carries the type/message/stack trace for the operator; no request body, header,
        // or credential is recorded (requirement A6 §8.5). A source-generated LoggerMessage
        // delegate is used (not LoggerExtensions.LogError) for allocation-free logging (CA1848).
        LogUnhandledException(correlationId, exception);

        ProblemDetails problem = ProblemDetailsBuilder.Unexpected(correlationId);

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.Headers[CorrelationId.HeaderName] = correlationId;

        // Delegate the write to the framework ProblemDetails service so the body honors the
        // configured content type and the shared customization (which re-stamps the correlation id
        // and errorCode). Returning true marks the exception handled; false would let it propagate.
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    /// <summary>
    /// The single structured log event for an unhandled exception, emitted via a source-generated
    /// high-performance delegate. It records the correlation id and the exception (type, message,
    /// stack trace) for the operator; it deliberately captures no request body, header, credential,
    /// or other PII (requirement A6 §8.5).
    /// </summary>
    /// <param name="correlationId">The correlation id shared with the client's error response.</param>
    /// <param name="exception">The unhandled exception captured server-side only.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing request. CorrelationId={CorrelationId}")]
    private partial void LogUnhandledException(string correlationId, Exception exception);
}
