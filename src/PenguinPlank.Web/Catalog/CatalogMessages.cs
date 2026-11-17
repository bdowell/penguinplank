using PenguinPlank.Web.Api;

namespace PenguinPlank.Web.Catalog;

/// <summary>Maps typed API failures to short, plain-language text for display.</summary>
public static class CatalogMessages
{
    /// <summary>Describes a failure to read data, where no change is at risk.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A caller-safe message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static string DescribeLoad(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind == ApiErrorKind.Connectivity
            ? "Could not reach the server. Check your connection, then choose Try again."
            : Describe(error);
    }

    /// <summary>Describes a failure for display.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A caller-safe message that says what happened and what to do next.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static string Describe(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        string message = error.Kind switch
        {
            ApiErrorKind.StaleVersion => "This record was changed by someone else after you opened it. Your change was not saved. Reload the latest version, then re-apply your change.",
            ApiErrorKind.Connectivity => "Could not reach the server. Your change is not saved and nothing is queued. Check your connection, then choose Try again.",
            ApiErrorKind.Unauthenticated => "Your session has ended. Sign in again, then retry.",
            ApiErrorKind.Forbidden => "You do not have permission to do this.",
            ApiErrorKind.NotFound => "This record no longer exists.",
            ApiErrorKind.Validation => string.IsNullOrWhiteSpace(error.Detail) ? "The server rejected the values entered. Review the form and try again." : error.Detail,
            ApiErrorKind.Conflict => string.IsNullOrWhiteSpace(error.Detail) ? "The change conflicts with existing data, for example a duplicate value." : error.Detail,
            ApiErrorKind.RateLimited => "Too many requests. Wait a moment, then try again.",
            ApiErrorKind.Server => "The server hit an unexpected problem. The change may not be saved. Try again shortly.",
            _ => string.IsNullOrWhiteSpace(error.Detail) ? error.Title : error.Detail,
        };

        return string.IsNullOrWhiteSpace(error.CorrelationId)
            ? message
            : $"{message} (Reference: {error.CorrelationId})";
    }
}
