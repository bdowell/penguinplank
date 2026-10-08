using Microsoft.AspNetCore.Identity;

namespace PenguinPlank.Infrastructure.IdentityAdministration;

/// <summary>
/// The authenticated staff identity for Penguin Plank, backed by ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// <para>
/// Phase A authenticates <b>staff accounts only</b> and provides <b>no public registration</b>
/// (requirement A2 §5.9). Accounts are provisioned by an Owner: the first Owner through the
/// one-time bootstrap (task 4.2) and additional staff through owner-only administration
/// endpoints (task 9.3). This type adds no custom fields in Phase A — it exists so the Identity
/// store keys users with the same <see cref="Guid"/> primary-key convention as the rest of the
/// schema (<see cref="IdentityUser{TKey}"/> with a <see cref="Guid"/> key).
/// </para>
/// <para>
/// <see cref="AppUser"/> lives in Infrastructure because it is inseparable from the ASP.NET Core
/// Identity persistence framework (coding-standards §3). The role an authenticated account carries
/// is modeled independently in the Application layer as
/// <see cref="PenguinPlank.Application.Abstractions.Role"/>; it is resolved at the API boundary and
/// passed into use cases through <see cref="PenguinPlank.Application.Abstractions.ActorContext"/>
/// so Domain and Application logic never depend on Identity or <c>HttpContext</c>. The future
/// customer identity (requirement A2 §5.13) is a separate business record and is deliberately
/// <b>not</b> represented by this staff login type.
/// </para>
/// </remarks>
public class AppUser : IdentityUser<Guid>
{
}
