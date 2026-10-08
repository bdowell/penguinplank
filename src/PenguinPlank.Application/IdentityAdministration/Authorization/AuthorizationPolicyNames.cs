namespace PenguinPlank.Application.IdentityAdministration.Authorization;

/// <summary>
/// The named ASP.NET Core authorization policies that gate Phase A endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Phase A has exactly two role-based access tiers (requirement A2 §5.10): Owner-only
/// operations (financials, user administration, configuration, imports, integration and
/// credential controls) and operations open to either Staff or Owner (catalog and
/// operational work). These constants name the policies registered in the API composition
/// root so endpoints reference a single stable identifier rather than repeating role
/// expressions (coding-standards §5). The policies are applied to endpoints in task 9.
/// </para>
/// <para>
/// Policy enforcement is one of <b>two</b> independent guards. The second is field-level
/// allowlist projection (see <c>IRoleProjector{TSource,TResult}</c>): even when a Staff
/// caller is authorized to read a catalog resource, the response is projected to a
/// role-safe shape so financial fields are stripped server-side (requirement A2 §5.11).
/// Hiding a control in the browser is never the sole guard.
/// </para>
/// </remarks>
public static class AuthorizationPolicyNames
{
    /// <summary>
    /// Requires the <see cref="RoleNames.Owner"/> role. Applied to financial, user-admin,
    /// configuration, import, and integration/credential operations.
    /// </summary>
    public const string OwnerOnly = "OwnerOnly";

    /// <summary>
    /// Requires either the <see cref="RoleNames.Staff"/> or <see cref="RoleNames.Owner"/>
    /// role. Applied to catalog and operational endpoints that both roles may use (with
    /// Owner-only fields still stripped from Staff responses by projection).
    /// </summary>
    public const string StaffOrOwner = "StaffOrOwner";
}
