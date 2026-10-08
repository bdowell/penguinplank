using PenguinPlank.Application.Abstractions;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// Provisions a staff account on behalf of an Owner and audits the creation (requirement A2 §5.9,
/// §5.10; A4 §6.7).
/// </summary>
/// <remarks>
/// <para>
/// The use case validates required inputs and delegates account creation (password-policy
/// validation, hashing, role assignment, and the redacted audit entry) to
/// <see cref="IStaffUserStore"/>, supplying the authenticated actor and the clock-supplied audit
/// instant (coding-standards §1, §2). A duplicate email or a password that fails the Identity
/// policy is an expected <see cref="ErrorCode.Validation"/> failure returned without auditing.
/// There is no public self-registration path — this Owner-only use case is the sole way a staff
/// account is created (requirement A2 §5.9).
/// </para>
/// <para>
/// <b>Why the store audits.</b> ASP.NET Core Identity rows are excluded from the automatic
/// <c>SaveChanges</c> audit interceptor (they carry secrets/PII), so account creation is audited
/// explicitly. The store owns the database context and persists both the account and the redacted
/// audit entry, so the entry is built from the pure audit-entry factory and written where the
/// context lives (coding-standards §3).
/// </para>
/// </remarks>
public sealed class CreateStaffUserUseCase
{
    private readonly IStaffUserStore _staffUserStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="staffUserStore">The staff-account administration boundary.</param>
    /// <param name="timeProvider">The injected clock supplying the audit instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public CreateStaffUserUseCase(
        IStaffUserStore staffUserStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(staffUserStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _staffUserStore = staffUserStore;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the staff-account creation.</summary>
    /// <param name="request">The decided account to create.</param>
    /// <param name="actor">The authenticated Owner performing the creation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the created account's role-safe view, or a
    /// <see cref="ErrorCode.Validation"/> failure for missing inputs, a duplicate email, or a
    /// password-policy violation (nothing is audited on failure).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<StaffUserView>> ExecuteAsync(
        CreateStaffUserRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result.Failure<StaffUserView>(ErrorCode.Validation, "A staff account requires an email.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure<StaffUserView>(ErrorCode.Validation, "A staff account requires an initial password.");
        }

        DateTimeOffset auditedAtUtc = _timeProvider.GetUtcNow();

        return await _staffUserStore
            .CreateAsync(request, actor, auditedAtUtc, cancellationToken)
            .ConfigureAwait(false);
    }
}
