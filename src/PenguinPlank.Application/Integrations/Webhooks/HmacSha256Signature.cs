using System.Security.Cryptography;

namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// Pure HMAC-SHA256 signature check shared by provider verifiers.
/// </summary>
/// <remarks>
/// Accepts the signature as hex or base64, with an optional <c>sha256=</c> prefix, and compares in
/// constant time. A missing or malformed signature is simply invalid.
/// </remarks>
public static class HmacSha256Signature
{
    private const string Prefix = "sha256=";

    /// <summary>Determines whether a signature header matches the HMAC-SHA256 of the body.</summary>
    /// <param name="secret">The shared secret.</param>
    /// <param name="body">The raw body the signature covers.</param>
    /// <param name="signature">The signature header value, or null.</param>
    /// <returns><see langword="true"/> when the signature matches.</returns>
    public static bool IsValid(
        System.ReadOnlySpan<byte> secret,
        System.ReadOnlySpan<byte> body,
        string? signature)
    {
        if (secret.IsEmpty || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        string text = signature.Trim();
        if (text.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase))
        {
            text = text[Prefix.Length..];
        }

        System.Span<byte> provided = stackalloc byte[HMACSHA256.HashSizeInBytes];
        if (!TryDecode(text, provided))
        {
            return false;
        }

        System.Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(secret, body, expected);

        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static bool TryDecode(string text, System.Span<byte> destination)
    {
        if (text.Length == HMACSHA256.HashSizeInBytes * 2
            && text.All(char.IsAsciiHexDigit))
        {
            System.Convert.FromHexString(text).CopyTo(destination);
            return true;
        }

        return System.Convert.TryFromBase64String(text, destination, out int baseWritten)
            && baseWritten == destination.Length;
    }
}
