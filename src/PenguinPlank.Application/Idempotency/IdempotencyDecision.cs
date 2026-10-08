namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The pure business rule that decides, when an idempotency key already has a persisted record,
/// whether an incoming request is a replay or a conflict (requirements A4 §6.3, §6.4).
/// </summary>
/// <remarks>
/// <para>
/// This is a pure function with no I/O, clock, or ambient state, so it is directly unit- and
/// property-testable with ordinary values (coding-standards §1, §7). Keeping the decision here —
/// rather than inside the EF store — is what lets Property 13 (idempotent command replay) assert
/// the replay-versus-conflict choice without a database: the store merely reads the existing hash
/// and stored result and asks this rule for the outcome.
/// </para>
/// <para>
/// <b>Rule.</b> Given the existing payload hash on the stored record and the incoming payload
/// hash on the retry: an identical hash yields a
/// <see cref="IdempotencyOutcomeKind.Replay"/> carrying the stored result (no repeated effect);
/// a differing hash yields a <see cref="IdempotencyOutcomeKind.Conflict"/>. Hash comparison is
/// case-insensitive via <see cref="PayloadHash"/> equality.
/// </para>
/// </remarks>
public static class IdempotencyDecision
{
    /// <summary>
    /// Decides the outcome for a request whose key already has a persisted record.
    /// </summary>
    /// <param name="existingHash">The payload hash stored against the existing record.</param>
    /// <param name="incomingHash">The payload hash of the current request reusing the key.</param>
    /// <param name="storedResult">
    /// The result reference stored against the existing record, or <see langword="null"/> when the
    /// original command reserved the key but has not yet completed. Carried through on a replay.
    /// </param>
    /// <returns>
    /// <see cref="IdempotencyOutcome.Replay(ResultRef?)"/> when the hashes match; otherwise
    /// <see cref="IdempotencyOutcome.Conflict"/>.
    /// </returns>
    public static IdempotencyOutcome ForExisting(
        PayloadHash existingHash,
        PayloadHash incomingHash,
        ResultRef? storedResult)
    {
        return existingHash == incomingHash
            ? IdempotencyOutcome.Replay(storedResult)
            : IdempotencyOutcome.Conflict;
    }
}
