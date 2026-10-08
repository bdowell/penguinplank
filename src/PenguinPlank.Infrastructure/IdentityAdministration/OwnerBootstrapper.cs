using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PenguinPlank.Application.IdentityAdministration;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Infrastructure.IdentityAdministration;

/// <summary>
/// ASP.NET Core Identity implementation of the one-time, idempotent Owner bootstrap
/// (requirement A2 §5.8).
/// </summary>
/// <remarks>
/// <para>
/// This adapter is the boundary that owns the Identity side effects: ensuring the Owner and
/// Staff roles exist and creating the first Owner account. The business decisions it depends on
/// — "is a password an obvious default?" — live in the pure
/// <see cref="OwnerBootstrapPasswordPolicy"/> so they are testable without Identity or a database
/// (coding-standards §1). Password <em>strength</em> is validated against the configured Identity
/// password policy that the injected <see cref="UserManager{TUser}"/> enforces, so the one-time
/// Owner is held to the same policy as every other account.
/// </para>
/// <para>
/// All dependencies are supplied through the constructor; it validates them and performs no I/O
/// (coding-standards §2). The service is registered scoped because
/// <see cref="UserManager{TUser}"/>/<see cref="RoleManager{TRole}"/> and the backing
/// <c>DbContext</c> are scoped. Log events use source-generated <see cref="LoggerMessage"/>
/// delegates (CA1848) and record only stable, secret-free detail — never the submitted password.
/// </para>
/// </remarks>
public sealed partial class OwnerBootstrapper : IOwnerBootstrapper
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly OwnerBootstrapOptions _options;
    private readonly ILogger<OwnerBootstrapper> _logger;

    /// <summary>
    /// Creates the bootstrapper with its required Identity dependencies and operator-supplied
    /// options. The constructor only validates and stores its dependencies.
    /// </summary>
    /// <param name="userManager">The Identity user manager used to query and create the Owner.</param>
    /// <param name="roleManager">The Identity role manager used to ensure the Owner/Staff roles.</param>
    /// <param name="options">The operator-supplied bootstrap options (email + initial password).</param>
    /// <param name="logger">A logger for structured, secret-free bootstrap events.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public OwnerBootstrapper(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<OwnerBootstrapOptions> options,
        ILogger<OwnerBootstrapper> logger)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<OwnerBootstrapOutcome>> EnsureOwnerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Idempotency: if an Owner already exists this is a safe no-op. The role-existence check
        // guards against the roles not yet being seeded on a brand-new database.
        if (await _roleManager.RoleExistsAsync(RoleNames.Owner).ConfigureAwait(false))
        {
            IList<AppUser> existingOwners =
                await _userManager.GetUsersInRoleAsync(RoleNames.Owner).ConfigureAwait(false);

            if (existingOwners.Count > 0)
            {
                LogSkippedOwnerExists();
                return Result.Success(OwnerBootstrapOutcome.AlreadyExists);
            }
        }

        // No default production password: refuse a missing/blank/placeholder value before touching
        // the store (requirement A2 §5.8). The refusal rule is the pure policy.
        if (OwnerBootstrapPasswordPolicy.IsRefusedDefault(_options.InitialPassword))
        {
            LogRefusedMissingPassword();
            return Result.Failure<OwnerBootstrapOutcome>(
                ErrorCode.Validation,
                "Owner bootstrap requires an operator-supplied initial password; " +
                "a missing or placeholder value is refused and no default is used.");
        }

        if (string.IsNullOrWhiteSpace(_options.Email))
        {
            LogRefusedMissingEmail();
            return Result.Failure<OwnerBootstrapOutcome>(
                ErrorCode.Validation,
                "Owner bootstrap requires an operator-supplied Owner email.");
        }

        // Guaranteed non-null by the checks above; captured locally for clarity.
        string ownerEmail = _options.Email!;
        string initialPassword = _options.InitialPassword!;

        Result rolesEnsured = await EnsureRolesExistAsync().ConfigureAwait(false);
        if (rolesEnsured.IsFailure)
        {
            return Result.Failure<OwnerBootstrapOutcome>(rolesEnsured.Error);
        }

        var owner = new AppUser
        {
            UserName = ownerEmail,
            Email = ownerEmail,
            EmailConfirmed = true,
        };

        // CreateAsync validates the password against the configured Identity password policy and
        // hashes it; the plaintext is never persisted or logged.
        IdentityResult createResult =
            await _userManager.CreateAsync(owner, initialPassword).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            LogRefusedPolicyViolation(DescribeIdentityErrors(createResult));
            return Result.Failure<OwnerBootstrapOutcome>(
                ErrorCode.Validation,
                "The supplied Owner credentials did not satisfy the account policy.");
        }

        IdentityResult roleAssignment =
            await _userManager.AddToRoleAsync(owner, RoleNames.Owner).ConfigureAwait(false);
        if (!roleAssignment.Succeeded)
        {
            // The account was created but could not be made an Owner. Surface a failure rather
            // than leave a non-Owner account masquerading as a successful bootstrap.
            LogRoleAssignmentFailed(DescribeIdentityErrors(roleAssignment));
            return Result.Failure<OwnerBootstrapOutcome>(
                ErrorCode.Validation,
                "The Owner account was created but could not be assigned the Owner role.");
        }

        LogOwnerCreated();
        return Result.Success(OwnerBootstrapOutcome.Created);
    }

    /// <summary>
    /// Ensures both the Owner and Staff roles exist, creating any that are missing. Idempotent:
    /// a role that already exists is left unchanged.
    /// </summary>
    private async Task<Result> EnsureRolesExistAsync()
    {
        foreach (string roleName in new[] { RoleNames.Owner, RoleNames.Staff })
        {
            if (await _roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
            {
                continue;
            }

            IdentityResult created =
                await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName)).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                LogRoleCreationFailed(roleName, DescribeIdentityErrors(created));
                return Result.Failure(
                    ErrorCode.Validation,
                    "A required role could not be created during Owner bootstrap.");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Produces a compact, secret-free description of Identity error codes for logging. Only the
    /// stable error codes are included — never the submitted password or other sensitive input.
    /// </summary>
    private static string DescribeIdentityErrors(IdentityResult result)
    {
        return string.Join(", ", result.Errors.Select(error => error.Code));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Owner bootstrap skipped: an Owner account already exists; no change made.")]
    private partial void LogSkippedOwnerExists();

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Owner bootstrap refused: no operator-supplied initial password was provided " +
                  "(a missing, blank, or placeholder value is not accepted; there is no default).")]
    private partial void LogRefusedMissingPassword();

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Owner bootstrap refused: no operator-supplied Owner email was provided.")]
    private partial void LogRefusedMissingEmail();

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Owner bootstrap refused: the supplied credentials did not satisfy the Identity " +
                  "policy ({ErrorCodes}).")]
    private partial void LogRefusedPolicyViolation(string errorCodes);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Owner bootstrap failed: created the account but could not assign the Owner role " +
                  "({ErrorCodes}).")]
    private partial void LogRoleAssignmentFailed(string errorCodes);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Owner bootstrap failed: could not create the {RoleName} role ({ErrorCodes}).")]
    private partial void LogRoleCreationFailed(string roleName, string errorCodes);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Owner bootstrap completed: created the first Owner account.")]
    private partial void LogOwnerCreated();
}
