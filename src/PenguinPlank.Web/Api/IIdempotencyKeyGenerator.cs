namespace PenguinPlank.Web.Api;

/// <summary>
/// Supplies the <c>Idempotency-Key</c> for a mutating request, so tests can control the value.
/// </summary>
public interface IIdempotencyKeyGenerator
{
    /// <summary>Creates a new, unique idempotency key.</summary>
    /// <returns>A non-empty key.</returns>
    string NewKey();
}
