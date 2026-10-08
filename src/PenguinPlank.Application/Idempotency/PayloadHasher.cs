using System.Security.Cryptography;
using System.Text;

namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// Computes the stable <see cref="PayloadHash"/> of a mutating command's request payload.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hashing approach.</b> The payload is hashed as the lowercase hex encoding of the SHA-256
/// digest of its canonical UTF-8 bytes. SHA-256 is deterministic and collision-resistant for this
/// purpose, and the fixed 64-character hex output always fits the persisted
/// <c>IdempotencyRecord.PayloadHash</c> column. The hash is used only to decide replay versus
/// conflict for an identical key (requirements A4 §6.3, §6.4); it is not a security token.
/// </para>
/// <para>
/// <b>Canonical bytes are the caller's responsibility.</b> Two logically identical payloads must
/// produce identical bytes to hash equal. The API boundary therefore passes a canonical
/// representation of the request (for example a stably ordered serialization) to
/// <see cref="Compute(System.ReadOnlySpan{byte})"/>, or the already-canonical UTF-8 text to
/// <see cref="Compute(string)"/>. This type does not define canonicalization; it hashes exactly
/// the bytes it is given, which keeps it a pure, deterministic function that is directly testable
/// without starting the application (coding-standards §1).
/// </para>
/// </remarks>
public static class PayloadHasher
{
    /// <summary>
    /// Computes the SHA-256 payload hash of the given canonical bytes.
    /// </summary>
    /// <param name="canonicalPayload">The canonical byte representation of the request payload.</param>
    /// <returns>The lowercase hex SHA-256 digest as a <see cref="PayloadHash"/>.</returns>
    public static PayloadHash Compute(System.ReadOnlySpan<byte> canonicalPayload)
    {
        System.Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(canonicalPayload, digest);

        // Lowercase hex keeps the stored value stable and case-consistent; PayloadHash equality is
        // case-insensitive regardless, so a differently-cased hash from another source still matches.
        return new PayloadHash(System.Convert.ToHexStringLower(digest));
    }

    /// <summary>
    /// Computes the SHA-256 payload hash of the given canonical text, encoded as UTF-8.
    /// </summary>
    /// <param name="canonicalPayload">The canonical UTF-8 text representation of the request payload.</param>
    /// <returns>The lowercase hex SHA-256 digest as a <see cref="PayloadHash"/>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="canonicalPayload"/> is null.</exception>
    public static PayloadHash Compute(string canonicalPayload)
    {
        System.ArgumentNullException.ThrowIfNull(canonicalPayload);
        return Compute(Encoding.UTF8.GetBytes(canonicalPayload));
    }
}
