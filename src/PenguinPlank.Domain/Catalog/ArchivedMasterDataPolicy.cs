using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The pure business policy that decides whether a <b>new</b> transaction may target catalog
/// master data (a <c>Product</c>, <c>ProductVariant</c>, or <c>ProductPiece</c>) given the
/// active-versus-archived state of the record(s) that transaction references.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> Archiving catalog master data is a soft retirement, not a delete
/// (requirement R01 / 1.8, design invariant 14). Two rules follow and this policy expresses the
/// first of them as a <em>decision</em>:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Archived blocks new.</b> A new transaction — a sale, stock movement, production record, a new
/// linkage, or any other fresh business action — must be rejected when it targets an archived
/// (inactive) record. An archived record is no longer a valid target for new activity.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>History stays readable.</b> Archiving never invalidates <em>existing</em> historical
/// references to the record. Prior sales, ledger entries, and linkages that already point at the
/// archived record remain readable and valid. This policy governs only the admission of
/// <em>new</em> transactions; it never inspects, rejects, or mutates existing history, so that
/// readability is preserved simply by this guard not applying to it.
/// </description>
/// </item>
/// </list>
/// <para>
/// The active-versus-archived state maps directly to the <c>ActiveFlag</c> carried by
/// <c>Product</c>, <c>ProductVariant</c>, and <c>ProductPiece</c>: an active record has
/// <see langword="true"/>, an archived record has <see langword="false"/>. The policy receives that
/// state as an <b>explicit boolean input</b> (a single flag, or one flag per referenced record for
/// a transaction that spans several) and returns a <see cref="Result"/>. It performs no I/O, loads
/// no entities, queries no database, and mutates nothing (coding-standards §1, §3). The calling use
/// case reads the <c>ActiveFlag</c> of each referenced record, invokes the guard, and on failure
/// records nothing — leaving all state, including existing history, unchanged (requirement 1.8).
/// </para>
/// <para>
/// <b>Multiple referenced records.</b> A new transaction may reference several master-data records
/// at once (for example a sale line pointing at both a variant and a specific piece). Such a
/// transaction is admissible only when <em>every</em> referenced record is active; a single
/// archived target rejects the whole transaction. A transaction that references no master data has
/// nothing to block and is admitted.
/// </para>
/// <para>
/// This is a pure static function tested directly with ordinary values — no application startup
/// required (coding-standards §7). Property 6 (task 7.12) exercises it across many inputs.
/// </para>
/// </remarks>
public static class ArchivedMasterDataPolicy
{
    /// <summary>
    /// Decides whether a new transaction may target a single catalog master-data record given
    /// whether that record is active.
    /// </summary>
    /// <param name="isTargetActive">
    /// <see langword="true"/> when the referenced record's <c>ActiveFlag</c> is set (the record is
    /// active); <see langword="false"/> when the record is archived. Archiving blocks new
    /// transactions but preserves the readability of existing historical references (requirement
    /// 1.8).
    /// </param>
    /// <returns>
    /// <see cref="Result.Success()"/> when the target is active; otherwise a failure carrying
    /// <see cref="ErrorCode.ArchivedRecord"/>. On failure the caller records no new transaction and
    /// leaves existing history unchanged.
    /// </returns>
    public static Result CanTransact(bool isTargetActive) =>
        isTargetActive
            ? Result.Success()
            : Result.Failure(
                ErrorCode.ArchivedRecord,
                "The transaction cannot be recorded because it targets archived master data; existing history remains readable.");

    /// <summary>
    /// Decides whether a new transaction may proceed when it references several catalog master-data
    /// records, given the active state of each.
    /// </summary>
    /// <param name="referencedTargetsActive">
    /// The <c>ActiveFlag</c> of each master-data record the new transaction references, in any
    /// order. The transaction is admissible only when every entry is <see langword="true"/>; a
    /// single <see langword="false"/> entry (an archived target) rejects the whole transaction. A
    /// <see langword="null"/> or empty collection represents a transaction that references no master
    /// data and is admitted.
    /// </param>
    /// <returns>
    /// <see cref="Result.Success()"/> when every referenced record is active (or none is
    /// referenced); otherwise a failure carrying <see cref="ErrorCode.ArchivedRecord"/>. On failure
    /// the caller records no new transaction and leaves existing history unchanged.
    /// </returns>
    public static Result CanTransact(IEnumerable<bool>? referencedTargetsActive)
    {
        if (referencedTargetsActive is null)
        {
            return Result.Success();
        }

        foreach (bool isTargetActive in referencedTargetsActive)
        {
            if (!isTargetActive)
            {
                return CanTransact(isTargetActive: false);
            }
        }

        return Result.Success();
    }
}
