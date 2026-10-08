namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The three ways a <see cref="IIdempotencyStore.BeginAsync"/> call for a key can resolve.
/// </summary>
/// <remarks>
/// This classifies the outcome so the caller can branch without inspecting nullable fields.
/// It is carried by <see cref="IdempotencyOutcome"/> and decided purely by
/// <see cref="IdempotencyDecision"/> (requirements A4 §6.2–§6.4).
/// </remarks>
public enum IdempotencyOutcomeKind
{
    /// <summary>
    /// The key was not seen before (a fresh record was reserved). The caller proceeds to execute
    /// the command once, then records the result with <see cref="IIdempotencyStore.CompleteAsync"/>.
    /// </summary>
    New = 0,

    /// <summary>
    /// The key was already used with an identical payload hash. The caller replays the stored
    /// result and performs no repeated effect (requirement A4 §6.4).
    /// </summary>
    Replay = 1,

    /// <summary>
    /// The key was already used with a different payload hash. The caller rejects the request as a
    /// conflict (requirement A4 §6.3), which maps to HTTP 409.
    /// </summary>
    Conflict = 2,
}
