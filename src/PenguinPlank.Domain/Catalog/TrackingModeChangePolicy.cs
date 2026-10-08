using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The pure business policy that decides whether a <see cref="ProductVariant"/> may change its
/// <see cref="TrackingMode"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> Once a variant has recorded stock history, its tracking mode is
/// locked: switching between <see cref="TrackingMode.Serialized"/> and
/// <see cref="TrackingMode.Quantity"/> would reinterpret existing ledger history and corrupt it
/// (requirement R01 / 1.13, design invariant 6). The only sanctioned way past the lock is a
/// <em>designed migration</em> — a deliberate, reviewed data conversion — which the caller signals
/// via the <c>migrationApplied</c> input. This policy expresses that rule as a <em>decision</em>
/// only. It receives the current and requested modes together with two facts about the variant as
/// <b>explicit inputs</b> and returns a <see cref="Result"/>; it performs no I/O, queries no
/// database, and mutates nothing (coding-standards §1, §3). The calling use case supplies whether
/// stock history exists and whether a designed migration applies, and is responsible for the
/// subsequent persistence; on rejection it leaves both the tracking mode and the stock history
/// unchanged (requirement 1.13).
/// </para>
/// <para>
/// <b>Decision truth table.</b>
/// <list type="table">
/// <listheader>
/// <term>Condition</term>
/// <description>Outcome</description>
/// </listheader>
/// <item>
/// <term><c>current</c> == <c>requested</c> (no change)</term>
/// <description>Success — a no-op is always permitted, regardless of history or migration.</description>
/// </item>
/// <item>
/// <term>Change requested, <c>hasStockHistory</c> is <see langword="false"/></term>
/// <description>Success — with no history to reinterpret the mode is free to change.</description>
/// </item>
/// <item>
/// <term>Change requested, <c>hasStockHistory</c> is <see langword="true"/>, <c>migrationApplied</c> is <see langword="true"/></term>
/// <description>Success — the designed migration path is the sanctioned way past the lock.</description>
/// </item>
/// <item>
/// <term>Change requested, <c>hasStockHistory</c> is <see langword="true"/>, <c>migrationApplied</c> is <see langword="false"/></term>
/// <description>Failure (<see cref="ErrorCode.TrackingModeLocked"/>) — the mode is locked.</description>
/// </item>
/// </list>
/// </para>
/// <para>
/// This is a pure static function tested directly with ordinary values — no application startup
/// required (coding-standards §7). Property 5 (task 7.10) exercises it across many inputs.
/// </para>
/// </remarks>
public static class TrackingModeChangePolicy
{
    /// <summary>
    /// Decides whether a variant may move from <paramref name="current"/> to
    /// <paramref name="requested"/> tracking mode given its stock-history and migration state.
    /// </summary>
    /// <param name="current">The variant's current tracking mode.</param>
    /// <param name="requested">The tracking mode the caller wishes to set.</param>
    /// <param name="hasStockHistory">
    /// <see langword="true"/> when the variant already has recorded stock history (inventory
    /// movements, balances, or allocations). When <see langword="true"/>, a mode change is locked
    /// unless a designed migration applies.
    /// </param>
    /// <param name="migrationApplied">
    /// <see langword="true"/> when a designed migration has been applied that deliberately converts
    /// the variant's existing stock history to the requested mode. Only this sanctioned path unlocks
    /// a change once stock history exists.
    /// </param>
    /// <returns>
    /// <see cref="Result.Success()"/> when the change is a no-op, when no stock history exists, or
    /// when stock history exists and a designed migration has been applied; a failure carrying
    /// <see cref="ErrorCode.TrackingModeLocked"/> when a change is requested against a variant with
    /// stock history and no designed migration applies. On failure the caller leaves the tracking
    /// mode and stock history unchanged (requirement 1.13).
    /// </returns>
    public static Result CanChange(
        TrackingMode current,
        TrackingMode requested,
        bool hasStockHistory,
        bool migrationApplied)
    {
        if (current == requested)
        {
            return Result.Success();
        }

        if (!hasStockHistory || migrationApplied)
        {
            return Result.Success();
        }

        return Result.Failure(
            ErrorCode.TrackingModeLocked,
            "The ProductVariant tracking mode cannot change because stock history exists and no designed migration applies.");
    }
}
