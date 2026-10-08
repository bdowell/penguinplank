using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A deliberate change to a catalog record's <see cref="PublicationState"/>, requested
/// explicitly by an actor.
/// </summary>
/// <remarks>
/// Publication is never automatic: a transition only happens when a caller asks for one of
/// these actions (requirements 1.12, 2.6, 2.7 / design invariant 11). There is no action that
/// a use case, background worker, or production-completion step performs implicitly.
/// </remarks>
public enum PublicationAction
{
    /// <summary>
    /// Approve draft content for public exposure: <see cref="PublicationState.Draft"/> →
    /// <see cref="PublicationState.PublicApproved"/>. The single deliberate path to making
    /// content public (requirement 2.7).
    /// </summary>
    Publish = 0,

    /// <summary>
    /// Withdraw previously approved content from public exposure:
    /// <see cref="PublicationState.PublicApproved"/> → <see cref="PublicationState.Draft"/>.
    /// A deliberate owner action; it is modelled so that returning content to draft is also an
    /// explicit decision rather than a side effect of some other operation.
    /// </summary>
    Withdraw = 1,
}

/// <summary>
/// The pure business policy for a catalog record's publication lifecycle (requirements 1.12,
/// 2.6, 2.7 / R01, R10 — design invariant 11).
/// </summary>
/// <remarks>
/// <para>
/// This policy encodes two guarantees as pure functions over explicit inputs, so it is directly
/// testable with ordinary values and needs no database, clock, or application startup
/// (coding-standards §1, §3):
/// </para>
/// <list type="number">
///   <item><description>
///   <b>Default is Draft.</b> <see cref="InitialState"/> is the single source of the state a
///   newly created Product, ProductVariant, or ProductPiece receives, and it is always
///   <see cref="PublicationState.Draft"/> (requirement 1.12). A produced piece's story is created
///   in the same draft state and is not promoted when production completes (requirement 2.6).
///   <see cref="AssertDefaultForNewItem"/> lets a caller verify a candidate initial state honours
///   this rule.
///   </description></item>
///   <item><description>
///   <b>No automatic publication.</b> The <em>only</em> way a record reaches
///   <see cref="PublicationState.PublicApproved"/> is a caller explicitly requesting
///   <see cref="PublicationAction.Publish"/> through <see cref="Transition"/> (or the
///   <see cref="Publish"/> convenience method). The policy never promotes a record on its own,
///   and it rejects any transition that is not an allowed, explicitly requested move
///   (requirements 2.6, 2.7).
///   </description></item>
/// </list>
/// <para>
/// Allowed transitions:
/// </para>
/// <list type="table">
///   <item><term>Draft + Publish</term><description>→ PublicApproved (the deliberate publish).</description></item>
///   <item><term>PublicApproved + Withdraw</term><description>→ Draft (the deliberate withdrawal).</description></item>
///   <item><term>Draft + Withdraw</term><description>rejected — already unpublished.</description></item>
///   <item><term>PublicApproved + Publish</term><description>rejected — already public.</description></item>
/// </list>
/// <para>
/// A rejected transition returns a failure carrying <see cref="ErrorCode.Validation"/> and leaves
/// the caller's state unchanged (the policy is pure and mutates nothing). The caller — a use case —
/// is responsible for persistence, audit, and concurrency once a transition succeeds; the business
/// decision itself lives here, free of side effects.
/// </para>
/// </remarks>
public static class PublicationPolicy
{
    /// <summary>
    /// The publication state every newly created catalog record starts in. Always
    /// <see cref="PublicationState.Draft"/> (requirement 1.12); exposed as the single source of
    /// the default so no caller hard-codes it independently.
    /// </summary>
    public static PublicationState InitialState => PublicationState.Draft;

    /// <summary>
    /// Confirms that a candidate initial state for a new catalog record honours the default-Draft
    /// rule (requirement 1.12).
    /// </summary>
    /// <param name="candidateInitialState">
    /// The publication state a record is about to be created with.
    /// </param>
    /// <returns>
    /// Success when <paramref name="candidateInitialState"/> equals <see cref="InitialState"/>;
    /// otherwise a <see cref="ErrorCode.Validation"/> failure. Nothing is mutated.
    /// </returns>
    public static Result AssertDefaultForNewItem(PublicationState candidateInitialState)
    {
        return candidateInitialState == InitialState
            ? Result.Success()
            : Result.Failure(
                ErrorCode.Validation,
                "A newly created catalog record must start in the Draft publication state.");
    }

    /// <summary>
    /// Applies an explicitly requested publication transition, validating that it is an allowed
    /// move from the current state.
    /// </summary>
    /// <param name="current">The record's current publication state.</param>
    /// <param name="action">The deliberate action the caller is requesting.</param>
    /// <returns>
    /// Success carrying the resulting <see cref="PublicationState"/> when
    /// <paramref name="action"/> is a legal move from <paramref name="current"/>; otherwise a
    /// <see cref="ErrorCode.Validation"/> failure describing why the transition is not allowed.
    /// The function is pure: on failure nothing changes, and on success the caller applies the
    /// returned state.
    /// </returns>
    /// <remarks>
    /// Because a transition only occurs in response to an explicit <paramref name="action"/>, this
    /// method can never promote a record to <see cref="PublicationState.PublicApproved"/> on its
    /// own — satisfying the no-automatic-publication guarantee (requirements 2.6, 2.7).
    /// </remarks>
    public static Result<PublicationState> Transition(PublicationState current, PublicationAction action)
    {
        return action switch
        {
            PublicationAction.Publish when current == PublicationState.Draft =>
                Result.Success(PublicationState.PublicApproved),

            PublicationAction.Publish =>
                Result.Failure<PublicationState>(
                    ErrorCode.Validation,
                    "The record is already public-approved and cannot be published again."),

            PublicationAction.Withdraw when current == PublicationState.PublicApproved =>
                Result.Success(PublicationState.Draft),

            PublicationAction.Withdraw =>
                Result.Failure<PublicationState>(
                    ErrorCode.Validation,
                    "The record is not public-approved and cannot be withdrawn."),

            _ =>
                Result.Failure<PublicationState>(
                    ErrorCode.Validation,
                    "The requested publication action is not recognised."),
        };
    }

    /// <summary>
    /// Convenience for the deliberate publish transition: equivalent to
    /// <see cref="Transition"/> with <see cref="PublicationAction.Publish"/>.
    /// </summary>
    /// <param name="current">The record's current publication state.</param>
    /// <returns>
    /// Success carrying <see cref="PublicationState.PublicApproved"/> from
    /// <see cref="PublicationState.Draft"/>; otherwise a <see cref="ErrorCode.Validation"/>
    /// failure when the record is already public-approved.
    /// </returns>
    public static Result<PublicationState> Publish(PublicationState current) =>
        Transition(current, PublicationAction.Publish);

    /// <summary>
    /// Convenience for the deliberate withdrawal transition: equivalent to
    /// <see cref="Transition"/> with <see cref="PublicationAction.Withdraw"/>.
    /// </summary>
    /// <param name="current">The record's current publication state.</param>
    /// <returns>
    /// Success carrying <see cref="PublicationState.Draft"/> from
    /// <see cref="PublicationState.PublicApproved"/>; otherwise a
    /// <see cref="ErrorCode.Validation"/> failure when the record is not public-approved.
    /// </returns>
    public static Result<PublicationState> Withdraw(PublicationState current) =>
        Transition(current, PublicationAction.Withdraw);
}
