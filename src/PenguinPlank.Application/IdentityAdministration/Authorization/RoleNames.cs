using PenguinPlank.Application.Abstractions;

namespace PenguinPlank.Application.IdentityAdministration.Authorization;

/// <summary>
/// The canonical string names of the two Phase A authorization roles.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core Identity stores roles by name and ASP.NET Core authorization policies
/// match roles by name, while application and domain code reasons about the strongly
/// typed <see cref="Role"/> enum. These constants are the single source of truth that
/// keeps the two representations aligned: the Owner bootstrap (task 4.2), the role seed,
/// the authorization policies (this task), and the administration endpoints (task 9.3)
/// all refer to the same names rather than scattering string literals (coding-standards
/// §5). Treat the values as a stable contract — Identity rows and issued claims depend on
/// them, so they must not be renamed casually.
/// </para>
/// </remarks>
public static class RoleNames
{
    /// <summary>The role name for the Owner: full access including financial data and administration.</summary>
    public const string Owner = "Owner";

    /// <summary>The role name for Staff: operational access only, never financial projections.</summary>
    public const string Staff = "Staff";

    /// <summary>
    /// Maps a strongly typed <see cref="Role"/> to its canonical role-name string.
    /// </summary>
    /// <param name="role">The role to translate.</param>
    /// <returns>The canonical name for <paramref name="role"/>.</returns>
    /// <exception cref="System.ComponentModel.InvalidEnumArgumentException">
    /// Thrown when <paramref name="role"/> is not a defined <see cref="Role"/> value.
    /// </exception>
    public static string FromRole(Role role)
    {
        return role switch
        {
            Role.Owner => Owner,
            Role.Staff => Staff,
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(role),
                (int)role,
                typeof(Role)),
        };
    }
}
