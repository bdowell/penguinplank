using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The pure business policy that decides whether a candidate <see cref="ProductPiece.PieceCode"/>
/// is acceptable: optional, and — when supplied — unique across the existing piece codes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> A ProductPiece business piece code is <em>optional</em> and must
/// be unique across pieces <em>when supplied</em> (requirements R01 / 1.6, 1.7): a colliding
/// non-null code is rejected leaving every catalog record unchanged. This policy expresses that
/// rule as a <em>decision</em> only. It receives the candidate value and the collection of
/// existing codes as <b>explicit inputs</b> and returns a <see cref="Result"/>; it performs no
/// I/O, queries no database, and mutates nothing (coding-standards §1, §3). The calling use case
/// supplies the existing codes (from the repository); the database's filtered-unique index
/// (migration task 3.2 / 3.6) is the authoritative backstop under concurrency.
/// </para>
/// <para>
/// <b>Optionality (filtered uniqueness).</b> A null, empty, or whitespace candidate means
/// "no piece code supplied" and is always accepted — any number of pieces may have no code. Only
/// a supplied (non-blank) code participates in the uniqueness check, mirroring the filtered-unique
/// index that ignores nulls. Blank existing entries are likewise treated as absent and ignored.
/// </para>
/// <para>
/// <b>Normalization.</b> A supplied code is matched after trimming leading and trailing whitespace
/// and comparing <b>case-insensitively</b> using
/// <see cref="System.StringComparer.OrdinalIgnoreCase"/>, so <c>"P-001"</c>, <c>" p-001 "</c>, and
/// <c>"P-001"</c> are treated as the same code. The policy compares values; it does not rewrite
/// the caller's stored code.
/// </para>
/// <para>
/// This is a pure static function tested directly with ordinary values (coding-standards §7).
/// Property 1 (task 7.2) exercises it across many inputs.
/// </para>
/// </remarks>
public static class PieceCodePolicy
{
    /// <summary>
    /// Validates a candidate piece code: an absent code is accepted; a supplied code must not
    /// collide with an existing supplied piece code.
    /// </summary>
    /// <param name="candidatePieceCode">
    /// The business piece code proposed for a <see cref="ProductPiece"/>, or
    /// <see langword="null"/>/blank when no code is supplied. A blank value is accepted (multiple
    /// pieces may have no code).
    /// </param>
    /// <param name="existingPieceCodes">
    /// The piece codes already held by existing pieces. May include blanks (treated as absent) and
    /// may be empty; must not be <see langword="null"/>. Compared to
    /// <paramref name="candidatePieceCode"/> after trimming, case-insensitively.
    /// </param>
    /// <returns>
    /// <see cref="Result.Success()"/> when the code is absent or supplied-and-unique; a failure
    /// carrying <see cref="ErrorCode.DuplicatePieceCode"/> when a supplied code collides with an
    /// existing supplied code. On failure the caller leaves all catalog records unchanged
    /// (requirement 1.7).
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="existingPieceCodes"/> is <see langword="null"/>. A missing
    /// collection is a programming mistake, not an expected business failure, so it surfaces as an
    /// exception (coding-standards §6).
    /// </exception>
    public static Result ValidateUnique(
        string? candidatePieceCode,
        IReadOnlyCollection<string?> existingPieceCodes)
    {
        System.ArgumentNullException.ThrowIfNull(existingPieceCodes);

        // An absent piece code is always allowed (filtered uniqueness: multiple nulls are fine).
        if (string.IsNullOrWhiteSpace(candidatePieceCode))
        {
            return Result.Success();
        }

        string normalizedCandidate = candidatePieceCode.Trim();

        foreach (string? existingPieceCode in existingPieceCodes)
        {
            if (!string.IsNullOrWhiteSpace(existingPieceCode)
                && string.Equals(
                    existingPieceCode.Trim(),
                    normalizedCandidate,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(
                    ErrorCode.DuplicatePieceCode,
                    "The piece code matches an existing ProductPiece piece code.");
            }
        }

        return Result.Success();
    }
}
