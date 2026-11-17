namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>A provider HTTP request, expressed without any HTTP client types.</summary>
/// <param name="Method">The HTTP method, for example <c>GET</c>.</param>
/// <param name="Uri">The absolute request URI.</param>
/// <param name="Body">The request body, or <see langword="null"/> when there is none.</param>
public sealed record IntegrationHttpRequest(string Method, Uri Uri, string? Body);

/// <summary>A provider HTTP response.</summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="Body">The response body, or <see langword="null"/> when there is none.</param>
/// <param name="RetryAfter">The provider's requested wait before a retry, when it supplied one.</param>
public sealed record IntegrationHttpResponse(int StatusCode, string? Body, TimeSpan? RetryAfter);

/// <summary>
/// The injected HTTP transport that future connectors use to reach a provider (requirement 3.16).
/// Tests substitute a deterministic fixture.
/// </summary>
public interface IIntegrationHttpTransport
{
    /// <summary>Sends a request to a provider.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The provider response.</returns>
    Task<IntegrationHttpResponse> SendAsync(IntegrationHttpRequest request, CancellationToken cancellationToken);
}
