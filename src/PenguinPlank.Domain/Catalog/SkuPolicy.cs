using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The pure business policy that decides whether a candidate <see cref="ProductVariant.Sku"/>
/// is acceptable: present, well-formed, and unique across the existing variant SKUs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> SKU uniqueness is a business rule (requirements R01 / 1.1,
/// 1.2): a variant SKU must be non-empty and must not collide with an existing variant's SKU,
/// and a colliding request is rejected leaving every catalog record unchanged. This policy
/// expresses that rule as a <em>decision</em> only. It receives the candidate value and the
/// collection of existing SKUs as <b>explicit inputs</b> and returns a <see cref="Result"/>;
/// it performs no I/O, queries no database, and mutates nothing (coding-standards §1, §3). The
/// calling use case supplies the existing SKUs (from the repository) and is responsible for the
/// subsequent persistence; the database's unique index (migration task 3.2 / 3.6) remains the
/// authoritative backstop under concurrency.
/// </para>
/// <para>
/// <b>Normalization.</b> A SKU is matched after trimming leading and trailing whitespace and
/// comparing <b>case-insensitively</b> using <see cref="System.StringComparer.OrdinalIgnoreCase"/>.
/// So <c>"abc-1"</c>, <c>" abc-1 "</c>, and <c>"ABC-1"</c> are treated as the same code and a
/// candidate matching any of those existing values is rejected as a duplicate. This mirrors the
/// filtered-unique index behavior and prevents trivially-different casings from slipping past the
/// in-memory check. The policy compares values; it does not rewrite the caller's stored SKU.
/// </para>
/// <para>
/// This is a pure static function tested directly with ordinary values — no application startup
/// required (coding-standards §7). Property 1 (task 7.2) exercises it across many inputs.
/// </para>
/// </remarks>
public static class SkuPolicy
{
    /// <summary>
    /// Validates a candidate variant SKU for presence and uniqueness against the existing SKUs.
    /// </summary>
    /// <param name="candidateSku">
    /// The SKU proposed for a new (or re-coded) <see cref="ProductVariant"/>. Required: a null,
    /// empty, or whitespace value is rejected with <see cref="ErrorCode.Validation"/>.
    /// </param>
    /// <param name="existingSkus">
    /// The SKUs already held by existing variants. May be empty; must not be <see langword="null"/>.
    /// Compared to <paramref name="candidateSku"/> after trimming, case-insensitively.
    /// </param>
    /// <returns>
    /// <see cref="Result.Success()"/> when the SKU is present and does not collide; a failure
    /// carrying <see cref="ErrorCode.Validation"/> when it is absent, or
    /// <see cref="ErrorCode.DuplicateSku"/> when it collides with an existing SKU. On failure the
    /// caller leaves all catalog records unchanged (requirement 1.2).
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="existingSkus"/> is <see langword="null"/>. A missing collection
    /// is a programming mistake, not an expected business failure, so it surfaces as an exception
    /// (coding-standards §6).
    /// </exception>
    public static Result ValidateUnique(string? candidateSku, IReadOnlyCollection<string> existingSkus)
    {
        System.ArgumentNullException.ThrowIfNull(existingSkus);

        if (string.IsNullOrWhiteSpace(candidateSku))
        {
            return Result.Failure(
                ErrorCode.Validation,
                "A ProductVariant SKU is required and must not be empty.");
        }

        string normalizedCandidate = candidateSku.Trim();

        foreach (string existingSku in existingSkus)
        {
            if (existingSku is not null
                && string.Equals(
                    existingSku.Trim(),
                    normalizedCandidate,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(
                    ErrorCode.DuplicateSku,
                    "The SKU matches an existing ProductVariant SKU.");
            }
        }

        return Result.Success();
    }
}
