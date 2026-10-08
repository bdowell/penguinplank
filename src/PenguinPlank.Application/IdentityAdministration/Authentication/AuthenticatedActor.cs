using PenguinPlank.Application.Abstractions;

namespace PenguinPlank.Application.IdentityAdministration.Authentication;

/// <summary>
/// The identity established by a successful sign-in: the authenticated staff user's id, email, and
/// authorization role (requirement A5 §7.1, §7.3).
/// </summary>
/// <remarks>
/// <para>
/// This is the role-safe projection the login/current-actor surface returns to the browser — it
/// carries no credential, no password hash, and no security stamp. It is distinct from
/// <see cref="ActorContext"/>: <see cref="ActorContext"/> is the internal value passed into use
/// cases, whereas <see cref="AuthenticatedActor"/> is the small shape describing "who am I" that a
/// client can observe. Keeping the two separate means the authentication boundary never leaks a
/// framework principal or an Identity entity across the Application boundary (coding-standards §3).
/// </para>
/// </remarks>
public sealed record AuthenticatedActor
{
    /// <summary>Creates the authenticated-actor description.</summary>
    /// <param name="userId">The authenticated user's stable identifier; must be non-empty.</param>
    /// <param name="email">The authenticated user's email.</param>
    /// <param name="role">The authorization role the account carries.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is empty.</exception>
    public AuthenticatedActor(Guid userId, string email, Role role)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An authenticated actor requires a non-empty user id.", nameof(userId));
        }

        UserId = userId;
        Email = email;
        Role = role;
    }

    /// <summary>The authenticated user's stable identifier.</summary>
    public Guid UserId { get; }

    /// <summary>The authenticated user's email (and username).</summary>
    public string Email { get; }

    /// <summary>The authorization role the account carries.</summary>
    public Role Role { get; }
}
