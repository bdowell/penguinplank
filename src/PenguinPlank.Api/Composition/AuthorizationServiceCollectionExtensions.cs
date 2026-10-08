using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Projection;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the Penguin Plank authorization policies and the field-level projection
/// services in the composition root.
/// </summary>
/// <remarks>
/// <para>
/// This is kept as a <b>separate</b> extension method (rather than inline in
/// <see cref="ServiceCollectionExtensions"/>) so authorization wiring evolves independently
/// of the persistence/identity wiring and of the one-time Owner bootstrap registration,
/// reducing merge contention between parallel foundation tasks. It is still part of the one
/// composition root: no business code resolves these services at runtime (coding-standards §2).
/// </para>
/// <para>
/// Two independent guards are registered here (requirement A2 §5.10, §5.11):
/// </para>
/// <list type="number">
///   <item>
///     <description>
///     Named role policies (<see cref="AuthorizationPolicyNames.OwnerOnly"/> and
///     <see cref="AuthorizationPolicyNames.StaffOrOwner"/>) backed by the Owner/Staff roles.
///     Endpoints apply these in task 9.
///     </description>
///   </item>
///   <item>
///     <description>
///     Field-level allowlist projectors that strip Owner-only financial fields from Staff
///     responses server-side. These are stateless, pure services, so they are registered as
///     <b>singletons</b>: they capture no scoped dependency such as a <c>DbContext</c>, which
///     keeps DI lifetimes correct (requirement A2 §5.14).
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Owner/Staff authorization policies and the role-based projection
    /// services.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankAuthorization(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // Named role policies. These express the Owner-only vs Staff-or-Owner access tiers
        // (A2 §5.10) in one place; endpoints reference the policy names in task 9. The
        // fallback policy requires an authenticated user, so a private resource is never
        // served to an anonymous caller by default (A5 §7.4) — endpoints opt in to
        // AllowAnonymous explicitly (for example the provider webhook surface).
        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                AuthorizationPolicyNames.OwnerOnly,
                policy => policy.RequireRole(RoleNames.Owner))
            .AddPolicy(
                AuthorizationPolicyNames.StaffOrOwner,
                policy => policy.RequireRole(RoleNames.Owner, RoleNames.Staff))
            .SetFallbackPolicy(
                new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());

        // Field-level allowlist projectors. Registered as singletons because they are pure and
        // stateless and must not capture a scoped DbContext (A2 §5.14). Both the concrete type
        // and the generic projector contract are registered so use cases can depend on whichever
        // is clearer at the call site.
        services.AddSingleton<CatalogVariantProjector>();
        services.AddSingleton<
            IRoleProjector<CatalogVariantInternalView, CatalogVariantView>,
            CatalogVariantProjector>();

        // The response-level variant projector the /api/v1/variants endpoints route every read
        // through: it maps the ProductVariantView read model into a role-safe base shape for Staff
        // and the Owner-derived shape for an Owner, so a Staff variant response is structurally
        // incapable of carrying an Owner-only cost/margin/profit field (A2 §5.10, §5.11; Property
        // 11). Pure and stateless, so a singleton with no captured scoped DbContext (A2 §5.14).
        services.AddSingleton<CatalogVariantResponseProjector>();
        services.AddSingleton<
            IRoleProjector<ProductVariantView, CatalogVariantResponseView>,
            CatalogVariantResponseProjector>();

        return services;
    }
}
