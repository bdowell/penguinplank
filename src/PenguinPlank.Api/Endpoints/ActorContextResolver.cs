using System.Security.Claims;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Resolves the authenticated <see cref="ActorContext"/> from the current request's principal at
/// the API boundary, so neither the Application nor the Domain layer ever touches
/// <c>HttpContext</c> (coding-standards §2, §3; requirement A2 §5.12).
/// </summary>
/// <remarks>
/// <para>
/// The authenticated principal carries the staff user's identifier in the standard
/// <see cref="ClaimTypes.NameIdentifier"/> claim and the Owner/Staff role in the standard
/// <see cref="ClaimTypes.Role"/> claim (the names ASP.NET Core Identity issues). This resolver
/// reads those claims and builds the explicit actor value the use cases accept. It is deliberately
/// a small, pure translation over an already-resolved <see cref="ClaimsPrincipal"/>: the mapping
/// from claims to an <see cref="ActorContext"/> is exercised directly in a unit test without
/// starting the application, while the thin HTTP accessor delegates to it.
/// </para>
/// <para>
/// Endpoints run behind the fallback authenticated-user policy and the Owner/Staff role policies,
/// so an unauthenticated request is already challenged before an endpoint handler runs. This
/// resolver still fails loudly if it is somehow invoked without a usable identity, because that
/// would be a wiring mistake rather than an expected business outcome (coding-standards §6).
/// </para>
/// </remarks>
public static class ActorContextResolver
{
    /// <summary>
    /// Resolves the <see cref="ActorContext"/> for an authenticated principal.
    /// </summary>
    /// <param name="principal">The authenticated principal carrying the user-id and role claims.</param>
    /// <returns>The explicit actor context passed into use cases.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="principal"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the principal carries no usable user identifier or recognizable role — a wiring
    /// mistake, since authorization should have rejected the request first.
    /// </exception>
    public static ActorContext Resolve(ClaimsPrincipal principal)
    {
        System.ArgumentNullException.ThrowIfNull(principal);

        string? userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out Guid userId) || userId == Guid.Empty)
        {
            throw new System.InvalidOperationException(
                "The authenticated principal carries no usable user identifier.");
        }

        Role role = ResolveRole(principal);
        return new ActorContext(userId, role);
    }

    private static Role ResolveRole(ClaimsPrincipal principal)
    {
        // Owner authority supersedes Staff when a principal somehow carries both.
        if (principal.IsInRole(RoleNames.Owner))
        {
            return Role.Owner;
        }

        if (principal.IsInRole(RoleNames.Staff))
        {
            return Role.Staff;
        }

        throw new System.InvalidOperationException(
            "The authenticated principal carries no recognized Owner or Staff role.");
    }
}
