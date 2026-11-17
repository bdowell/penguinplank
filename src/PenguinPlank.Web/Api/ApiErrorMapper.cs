using System.Text.Json;

namespace PenguinPlank.Web.Api;

/// <summary>
/// Pure mapping from an HTTP failure (status and optional ProblemDetails body) or a transport
/// failure to a typed <see cref="ApiError"/>.
/// </summary>
public static class ApiErrorMapper
{
    /// <summary>The ProblemDetails extension holding the stable machine-readable code.</summary>
    public const string ErrorCodeMember = "errorCode";

    /// <summary>The ProblemDetails extension holding the per-request correlation id.</summary>
    public const string CorrelationIdMember = "correlationId";

    private const string StaleVersionCode = "StaleVersion";

    /// <summary>
    /// Maps a non-success HTTP response to a typed error. A malformed or empty body is tolerated:
    /// the error is then derived from the status code alone.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body, possibly empty or not JSON.</param>
    /// <returns>The typed error.</returns>
    public static ApiError FromResponse(int statusCode, string? body)
    {
        string? title = null;
        string? detail = null;
        string? errorCode = null;
        string? correlationId = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    title = ReadString(root, "title");
                    detail = ReadString(root, "detail");
                    errorCode = ReadString(root, ErrorCodeMember);
                    correlationId = ReadString(root, CorrelationIdMember);
                }
            }
            catch (JsonException)
            {
                // A non-JSON error body (for example from a proxy) is not a contract violation the
                // user can act on; fall back to the status code.
            }
        }

        ApiErrorKind kind = KindFor(statusCode, errorCode);
        return new ApiError(kind, statusCode, errorCode, title ?? DefaultTitle(kind), detail, correlationId);
    }

    /// <summary>
    /// Creates the typed error for a request that never received a response.
    /// </summary>
    /// <returns>A <see cref="ApiErrorKind.Connectivity"/> error.</returns>
    public static ApiError Connectivity() => new(
        ApiErrorKind.Connectivity,
        StatusCode: null,
        ErrorCode: null,
        Title: DefaultTitle(ApiErrorKind.Connectivity),
        Detail: "The server could not be reached. Your change was not saved. Check your connection and try again.",
        CorrelationId: null);

    /// <summary>
    /// Creates the typed error for a success response whose body could not be understood.
    /// </summary>
    /// <param name="statusCode">The HTTP status code received.</param>
    /// <returns>An <see cref="ApiErrorKind.Unexpected"/> error.</returns>
    public static ApiError UnreadableResponse(int statusCode) => new(
        ApiErrorKind.Unexpected,
        statusCode,
        ErrorCode: null,
        Title: DefaultTitle(ApiErrorKind.Unexpected),
        Detail: "The server response could not be read.",
        CorrelationId: null);

    private static ApiErrorKind KindFor(int statusCode, string? errorCode)
    {
        if (string.Equals(errorCode, StaleVersionCode, StringComparison.Ordinal))
        {
            return ApiErrorKind.StaleVersion;
        }

        return statusCode switch
        {
            400 => ApiErrorKind.Validation,
            401 => ApiErrorKind.Unauthenticated,
            403 => ApiErrorKind.Forbidden,
            404 => ApiErrorKind.NotFound,
            409 => ApiErrorKind.Conflict,
            412 => ApiErrorKind.StaleVersion,
            413 => ApiErrorKind.PayloadTooLarge,
            429 => ApiErrorKind.RateLimited,
            >= 500 => ApiErrorKind.Server,
            _ => ApiErrorKind.Unexpected,
        };
    }

    private static string DefaultTitle(ApiErrorKind kind) => kind switch
    {
        ApiErrorKind.Validation => "Invalid request",
        ApiErrorKind.Unauthenticated => "Authentication required",
        ApiErrorKind.Forbidden => "Forbidden",
        ApiErrorKind.NotFound => "Not found",
        ApiErrorKind.Conflict => "Conflict",
        ApiErrorKind.StaleVersion => "Precondition failed",
        ApiErrorKind.PayloadTooLarge => "Payload too large",
        ApiErrorKind.RateLimited => "Too many requests",
        ApiErrorKind.Connectivity => "Connection lost",
        ApiErrorKind.Server => "Internal server error",
        _ => "Error",
    };

    private static string? ReadString(JsonElement root, string name)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}
