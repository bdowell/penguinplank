using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.IdentityAdministration;

/// <summary>
/// The one-time, idempotent provisioning path that ensures the Owner and Staff roles exist
/// and creates the first Owner account when none exists (requirement A2 §5.8).
/// </summary>
/// <remarks>
/// <para>
/// This is an Application boundary interface (coding-standards §2, §3): the use case is defined
/// here and implemented in Infrastructure over ASP.NET Core Identity. Keeping it behind an
/// interface lets the invocation seam (a guarded startup/admin action) depend on an abstraction
/// and lets tests substitute a controllable implementation.
/// </para>
/// <para>
/// <b>Idempotency.</b> If an Owner already exists, the implementation makes no change and reports
/// <see cref="OwnerBootstrapOutcome.AlreadyExists"/>. Running the bootstrap repeatedly is safe.
/// </para>
/// <para>
/// <b>No default production password.</b> The initial Owner password comes only from the
/// operator-supplied <see cref="OwnerBootstrapOptions"/>. A missing, blank, or placeholder
/// password — or one that fails the configured Identity password policy — causes a refusal
/// (a failed <see cref="Result{T}"/> carrying <see cref="ErrorCode.Validation"/>), never a
/// fallback to a hardcoded secret.
/// </para>
/// </remarks>
public interface IOwnerBootstrapper
{
    /// <summary>
    /// Ensures the Owner/Staff roles exist and provisions the first Owner account when none
    /// exists, using the operator-supplied credentials.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous Identity operations.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> whose value distinguishes
    /// <see cref="OwnerBootstrapOutcome.Created"/> from
    /// <see cref="OwnerBootstrapOutcome.AlreadyExists"/>, or a failure carrying a stable error
    /// code when the supplied password is refused or fails the Identity policy.
    /// </returns>
    System.Threading.Tasks.Task<Result<OwnerBootstrapOutcome>> EnsureOwnerAsync(
        System.Threading.CancellationToken cancellationToken);
}
