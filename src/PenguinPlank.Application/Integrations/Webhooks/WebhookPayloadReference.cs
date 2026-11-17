using System.Security.Cryptography;
using System.Text;

namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// Pure rules for provider names and the deterministic payload reference of a delivery.
/// </summary>
public static class WebhookPayloadReference
{
    /// <summary>The maximum provider name length.</summary>
    public const int MaxProviderLength = 50;

    /// <summary>Determines whether a provider route segment is a safe lowercase token.</summary>
    /// <param name="provider">The provider route segment.</param>
    /// <returns><see langword="true"/> when it is 1 to 50 characters of letters, digits, or hyphen.</returns>
    public static bool IsValidProvider(string? provider)
    {
        if (string.IsNullOrEmpty(provider) || provider.Length > MaxProviderLength)
        {
            return false;
        }

        foreach (char c in provider)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds the payload reference for a provider's delivery key.</summary>
    /// <param name="provider">A provider name that satisfies <see cref="IsValidProvider"/>.</param>
    /// <param name="deliveryKey">The delivery key.</param>
    /// <returns>A reference of the form <c>webhooks/{provider}/{sha256-hex}</c>.</returns>
    public static PayloadRef For(string provider, DeliveryKey deliveryKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(deliveryKey.Value));
        string name = provider.ToLowerInvariant();
        return new PayloadRef($"webhooks/{name}/{System.Convert.ToHexStringLower(hash)}");
    }
}
