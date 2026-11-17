namespace PenguinPlank.Web.Api;

/// <summary>Generates idempotency keys from random GUIDs.</summary>
public sealed class GuidIdempotencyKeyGenerator : IIdempotencyKeyGenerator
{
    /// <inheritdoc />
    public string NewKey() => Guid.NewGuid().ToString("N");
}
