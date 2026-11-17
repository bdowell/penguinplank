namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// An inbound provider callback as received at the webhook boundary: the raw body bytes exactly
/// as sent (signatures are computed over them) and the request headers.
/// </summary>
/// <param name="Provider">The provider route segment, for example <c>shopify</c>.</param>
/// <param name="Body">The raw request body.</param>
/// <param name="Headers">The request headers, looked up case-insensitively.</param>
public sealed record WebhookCallback(
    string Provider,
    System.ReadOnlyMemory<byte> Body,
    IReadOnlyDictionary<string, string> Headers);
