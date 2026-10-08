using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// The narrow boundary for staff-account administration: list staff accounts and provision a new
/// one (requirement A2 §5.9, §5.10).
/// </summary>
/// <remarks>
/// <para>
/// A focused, responsibility-named interface defined in Application and implemented in
/// Infrastructure over ASP.NET Core Identity (coding-standards §2, §3). It returns the role-safe
/// <see cref="StaffUserView"/> read model that carries no credential, and creates accounts only
/// through the Owner-only path — there is no public self-registration surface anywhere
/// (requirement A2 §5.9). Every method takes and propagates a <see cref="CancellationToken"/>.
/// </para>
/// <para>
/// The implementation validates the password against the configured Identity policy and assigns
/// the requested role; a policy violation or duplicate email is an expected business failure
/// returned as a typed <see cref="Result"/> carrying <see cref="ErrorCode.Validation"/>, not an
/// exception. The store performs no authorization decision — the endpoints are gated Owner-only.
/// </para>
/// </remarks>
public interface IStaffUserStore
{
    /// <summary>
    /// Lists every staff account with its role.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The role-safe views of all staff accounts.</returns>
    Task<IReadOnlyList<StaffUserView>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Provisions a staff account from the supplied request, validating the password against the
    /// Identity policy, assigning the requested role, and recording a redacted audit entry.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core Identity rows are deliberately excluded from the automatic <c>SaveChanges</c>
    /// audit interceptor because they carry secrets/PII (requirement A4 §6.7). Account creation is
    /// therefore audited here with an explicit, redacted summary (actor, action, the new account's
    /// id — never the password), as the interceptor's documentation prescribes. The implementation
    /// builds that entry with the pure audit-entry factory from the supplied actor and timestamp.
    /// </remarks>
    /// <param name="request">The decided account to create.</param>
    /// <param name="actor">The authenticated Owner the audit entry attributes the creation to.</param>
    /// <param name="auditedAtUtc">The audit timestamp supplied from an injected clock.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success carrying the created account's role-safe view, or a
    /// <see cref="ErrorCode.Validation"/> failure when the email is duplicate or the password fails
    /// the policy.
    /// </returns>
    Task<Result<StaffUserView>> CreateAsync(
        CreateStaffUserRequest request,
        PenguinPlank.Application.Abstractions.ActorContext actor,
        DateTimeOffset auditedAtUtc,
        CancellationToken cancellationToken);
}
