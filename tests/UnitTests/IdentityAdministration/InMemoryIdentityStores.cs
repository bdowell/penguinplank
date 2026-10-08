using Microsoft.AspNetCore.Identity;
using PenguinPlank.Infrastructure.IdentityAdministration;

namespace UnitTests.IdentityAdministration;

/// <summary>
/// A hand-written, in-memory ASP.NET Core Identity user store used to exercise the real
/// <see cref="OwnerBootstrapper"/> orchestration without a database (coding-standards §7).
/// </summary>
/// <remarks>
/// <para>
/// It implements only the store surfaces the bootstrap path touches:
/// <see cref="IUserPasswordStore{TUser}"/> (CreateAsync + password hash),
/// <see cref="IUserEmailStore{TUser}"/> (the bootstrap sets email/username), and
/// <see cref="IUserRoleStore{TUser}"/> (GetUsersInRoleAsync / AddToRoleAsync). It is a
/// controllable substitute for the storage boundary, letting a real
/// <see cref="UserManager{TUser}"/> run its actual logic (password-policy validation, hashing,
/// role assignment) over deterministic state.
/// </para>
/// <para>
/// This proves <b>orchestration</b> only — idempotency, refusal, and role assignment. Relational
/// behavior (unique indexes, transactions) is proven by IntegrationTests against real SQL Server.
/// </para>
/// </remarks>
internal sealed class InMemoryUserStore
    : IUserPasswordStore<AppUser>,
        IUserEmailStore<AppUser>,
        IUserRoleStore<AppUser>
{
    private readonly List<AppUser> _users = new();
    private readonly Dictionary<Guid, HashSet<string>> _rolesByUserId = new();

    /// <summary>The number of users currently stored — used by tests to assert no duplicate Owner.</summary>
    public int UserCount => _users.Count;

    public Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id == Guid.Empty)
        {
            user.Id = Guid.NewGuid();
        }

        _users.Add(user);
        _rolesByUserId[user.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        _users.RemoveAll(candidate => candidate.Id == user.Id);
        _rolesByUserId.Remove(user.Id);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_users.FirstOrDefault(user => user.Id.ToString() == userId));
    }

    public Task<AppUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            _users.FirstOrDefault(user =>
                string.Equals(user.NormalizedUserName, normalizedUserName, StringComparison.Ordinal)));
    }

    public Task<string> GetUserIdAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.UserName);
    }

    public Task SetUserNameAsync(AppUser user, string? userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.NormalizedUserName);
    }

    public Task SetNormalizedUserNameAsync(AppUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    // IUserPasswordStore
    public Task SetPasswordHashAsync(AppUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.PasswordHash is not null);
    }

    // IUserEmailStore
    public Task SetEmailAsync(AppUser user, string? email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task SetEmailConfirmedAsync(AppUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task<AppUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            _users.FirstOrDefault(user =>
                string.Equals(user.NormalizedEmail, normalizedEmail, StringComparison.Ordinal)));
    }

    public Task<string?> GetNormalizedEmailAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.NormalizedEmail);
    }

    public Task SetNormalizedEmailAsync(AppUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    // IUserRoleStore
    public Task AddToRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!_rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles))
        {
            roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _rolesByUserId[user.Id] = roles;
        }

        roles.Add(roleName);
        return Task.CompletedTask;
    }

    public Task RemoveFromRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (_rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles))
        {
            roles.Remove(roleName);
        }

        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(AppUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        IList<string> roles = _rolesByUserId.TryGetValue(user.Id, out HashSet<string>? found)
            ? found.ToList()
            : new List<string>();
        return Task.FromResult(roles);
    }

    public Task<bool> IsInRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        bool inRole = _rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles)
            && roles.Contains(roleName);
        return Task.FromResult(inRole);
    }

    public Task<IList<AppUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        IList<AppUser> matches = _users
            .Where(user =>
                _rolesByUserId.TryGetValue(user.Id, out HashSet<string>? roles)
                && roles.Contains(roleName))
            .ToList();
        return Task.FromResult(matches);
    }

    public void Dispose()
    {
        // Nothing to release; state is purely in-memory.
    }
}

/// <summary>
/// A hand-written, in-memory ASP.NET Core Identity role store used alongside
/// <see cref="InMemoryUserStore"/> so a real <see cref="RoleManager{TRole}"/> can ensure the
/// Owner/Staff roles during bootstrap without a database (coding-standards §7).
/// </summary>
internal sealed class InMemoryRoleStore : IRoleStore<IdentityRole<Guid>>
{
    private readonly List<IdentityRole<Guid>> _roles = new();

    public Task<IdentityResult> CreateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (role.Id == Guid.Empty)
        {
            role.Id = Guid.NewGuid();
        }

        _roles.Add(role);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
    {
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        _roles.RemoveAll(candidate => candidate.Id == role.Id);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<string> GetRoleIdAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        return Task.FromResult(role.Id.ToString());
    }

    public Task<string?> GetRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        return Task.FromResult(role.Name);
    }

    public Task SetRoleNameAsync(IdentityRole<Guid> role, string? roleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<Guid> role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        return Task.FromResult(role.NormalizedName);
    }

    public Task SetNormalizedRoleNameAsync(IdentityRole<Guid> role, string? normalizedName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public Task<IdentityRole<Guid>?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_roles.FirstOrDefault(role => role.Id.ToString() == roleId));
    }

    public Task<IdentityRole<Guid>?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        return Task.FromResult(
            _roles.FirstOrDefault(role =>
                string.Equals(role.NormalizedName, normalizedRoleName, StringComparison.Ordinal)));
    }

    public void Dispose()
    {
        // Nothing to release; state is purely in-memory.
    }
}
