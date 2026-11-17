using System.Text;

namespace PenguinPlank.Web.Api;

/// <summary>Builds a URL query string, omitting parameters that have no value.</summary>
internal static class QueryStringBuilder
{
    /// <summary>
    /// Builds a query string from the supplied name/value pairs.
    /// </summary>
    /// <param name="parameters">The pairs; entries with a null or empty value are skipped.</param>
    /// <returns>The query string including the leading <c>?</c>, or an empty string when no pair remains.</returns>
    public static string Build(IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var builder = new StringBuilder();
        foreach (KeyValuePair<string, string?> pair in parameters)
        {
            if (string.IsNullOrEmpty(pair.Value))
            {
                continue;
            }

            builder.Append(builder.Length == 0 ? '?' : '&');
            builder.Append(Uri.EscapeDataString(pair.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(pair.Value));
        }

        return builder.ToString();
    }

    /// <summary>Formats a flag as a parameter value, omitting it when false.</summary>
    /// <param name="value">The flag.</param>
    /// <returns><c>true</c> when set; otherwise <see langword="null"/>.</returns>
    public static string? Flag(bool value) => value ? "true" : null;

    /// <summary>Formats a number invariantly.</summary>
    /// <param name="value">The optional number.</param>
    /// <returns>The invariant text, or <see langword="null"/>.</returns>
    public static string? Number(int? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Formats an identifier.</summary>
    /// <param name="value">The optional identifier.</param>
    /// <returns>The identifier text, or <see langword="null"/>.</returns>
    public static string? Id(Guid? value) =>
        value?.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
