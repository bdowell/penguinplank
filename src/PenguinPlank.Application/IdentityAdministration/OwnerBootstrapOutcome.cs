namespace PenguinPlank.Application.IdentityAdministration;

/// <summary>
/// The outcome of a one-time Owner bootstrap attempt (requirement A2 §5.8).
/// </summary>
/// <remarks>
/// The bootstrap is idempotent: it creates the Owner only when none exists and is a safe
/// no-op otherwise. The outcome is carried as the value of a successful
/// <see cref="PenguinPlank.Domain.Common.Result{T}"/>; a <em>refusal</em> (missing or
/// placeholder password, or a password that fails the Identity policy) is modeled as a
/// failed result with a stable error code, not as a value here.
/// </remarks>
public enum OwnerBootstrapOutcome
{
    /// <summary>
    /// No Owner existed, so the Owner and Staff roles were ensured and the Owner account
    /// was created from the operator-supplied credentials.
    /// </summary>
    Created = 0,

    /// <summary>
    /// An Owner already existed, so the bootstrap made no change. Re-running the bootstrap
    /// is safe and produces this outcome.
    /// </summary>
    AlreadyExists = 1,
}
