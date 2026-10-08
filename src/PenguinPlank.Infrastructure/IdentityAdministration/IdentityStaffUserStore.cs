using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Domain.Auditing;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.IdentityAdministration;

/// <summary>
/// The ASP.NET Core Identity implementation of <see cref="IStaffUserStore"/>: lists staff accounts
/// and provisions a new one on behalf of an Owner (requirement A2 §5.9, §5.10).
/// </summary>
/// <remarks>
/// <para>
/// This adapter owns the Identity side effects — creating the user, validating the password against
/// the configured policy (via <see cref="UserManager{TUser}"/>), hashing it, and assigning the
/// requested role. It maps the Identity user to the role-safe <see cref="StaffUserView"/> that
/// carries no credential; no Identity entity crosses the Application boundary (coding-standards §3).
/// A duplicate email or a password-policy violation is an <em>expected</em> business failure
/// returned as a typed <see cref="Result"/> carrying <see cref="ErrorCode.Validation"/> — never a
/// baked-in default and never an unexpected exception. Account creation here is the only staff
/// account-creation path; there is no public self-registration (requirement A2 §5.9).
/// </para>
/// <para>
/// Dependencies are supplied through the constructor. The service is registered <b>scoped</b>
/// because <see cref="UserManager{TUser}"/> and the backing <c>DbContext</c> are scoped
/// (coding-standards §2; requirement A2 §5.14). Log events use source-generated
/// <see cref="LoggerMessage"/> delegates and record only stable, secret-free detail — never the
/// submitted password.
/// </para>
/// </remarks>
public sealed partial class IdentityStaffUserStore : IStaffUserStore
{
    private readonly UserManager<AppUser> _userManager;
    private readonly PenguinPlankDbContext _dbContext;
    private readonly ILogger<IdentityStaffUserStore> _logger;

    /// <summary>Creates the store with its Identity and persistence dependencies.</summary>
    /// <param name="userManager">The Identity user manager used to query and create staff accounts.</param>
    /// <param name="dbContext">The scoped context used to persist the redacted account-creation audit entry.</param>
    /// <param name="logger">A logger for structured, secret-free administration events.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public IdentityStaffUserStore(
        UserManager<AppUser> userManager,
        PenguinPlankDbContext dbContext,
        ILogger<IdentityStaffUserStore> logger)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaffUserView>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<AppUser> users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var views = new List<StaffUserView>(users.Count);
        foreach (AppUser user in users)
        {
            IList<string> roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            views.Add(ToView(user, roles));
        }

        return views;
    }

    /// <inheritdoc />
    public async Task<Result<StaffUserView>> CreateAsync(
        CreateStaffUserRequest request,
        ActorContext actor,
        DateTimeOffset auditedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure<StaffUserView>(ErrorCode.Validation, "A staff account requires an email and an initial password.");
        }

        if (!Enum.IsDefined(request.Role))
        {
            return Result.Failure<StaffUserView>(ErrorCode.Validation, "The requested role is not a recognized role.");
        }

        AppUser? existing = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Failure<StaffUserView>(ErrorCode.Validation, "An account with that email already exists.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
        };

        IdentityResult createResult = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            LogRefusedPolicyViolation(DescribeIdentityErrors(createResult));
            return Result.Failure<StaffUserView>(ErrorCode.Validation, "The supplied credentials did not satisfy the account policy.");
        }

        string roleName = RoleNames.FromRole(request.Role);
        IdentityResult roleAssignment = await _userManager.AddToRoleAsync(user, roleName).ConfigureAwait(false);
        if (!roleAssignment.Succeeded)
        {
            // The account was created but could not be given its role. Remove it so we never leave a
            // roleless account behind, and surface the failure.
            await _userManager.DeleteAsync(user).ConfigureAwait(false);
            LogRoleAssignmentFailed(DescribeIdentityErrors(roleAssignment));
            return Result.Failure<StaffUserView>(ErrorCode.Validation, "The staff account could not be assigned its role.");
        }

        // Identity rows are excluded from the automatic SaveChanges audit interceptor (they carry
        // secrets/PII), so account creation is audited here with an explicit, redacted summary
        // (actor, action, the new account's id — never the password) built by the pure factory
        // (requirement A4 §6.7).
        AuditEntry auditEntry = AuditEntryFactory.CreateEntry(
            new AuditChange("StaffUser", user.Id, AuditOperation.Created),
            actor,
            auditedAtUtc);
        _dbContext.Set<AuditEntry>().Add(auditEntry);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogStaffAccountCreated();
        return Result.Success(ToView(user, [roleName]));
    }

    private static StaffUserView ToView(AppUser user, IEnumerable<string> roles)
    {
        // Owner authority supersedes Staff when a principal somehow carries both.
        Role role = roles.Contains(RoleNames.Owner, StringComparer.Ordinal) ? Role.Owner : Role.Staff;

        return new StaffUserView
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Role = role,
        };
    }

    private static string DescribeIdentityErrors(IdentityResult result)
    {
        return string.Join(", ", result.Errors.Select(error => error.Code));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Staff account creation refused: the supplied credentials did not satisfy the Identity policy ({ErrorCodes}).")]
    private partial void LogRefusedPolicyViolation(string errorCodes);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Staff account creation failed: created the account but could not assign its role ({ErrorCodes}); the account was removed.")]
    private partial void LogRoleAssignmentFailed(string errorCodes);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Staff account created by an Owner.")]
    private partial void LogStaffAccountCreated();
}
