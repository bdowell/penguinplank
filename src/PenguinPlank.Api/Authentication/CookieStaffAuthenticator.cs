using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Authentication;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.IdentityAdministration;

namespace PenguinPlank.Api.Authentication;

/// <summary>
/// The Phase A cookie-backed implementation of <see cref="IStaffAuthenticator"/>: it validates
/// staff credentials with ASP.NET Core Identity and establishes a secure, HttpOnly, same-origin
/// cookie session (requirements A2 §5.9, A5 §7.1, §7.2).
/// </summary>
/// <remarks>
/// <para>
/// Sign-in delegates to <see cref="SignInManager{TUser}"/>, which checks the password against the
/// configured policy, honors lockout, and — on success — signs the principal into the Identity
/// application cookie scheme. No bearer token is issued or returned; the browser carries only the
/// HttpOnly cookie (requirement A5 §7.2). Sign-out clears that cookie. The current-actor lookup
/// reads the already-authenticated principal from the current request.
/// </para>
/// <para>
/// <b>Why this is the swap point for OIDC (requirement A5 §7.3).</b> The login/logout endpoints
/// depend only on <see cref="IStaffAuthenticator"/>. When a mobile client is in scope, an
/// OIDC+PKCE-backed implementation of the same interface replaces this cookie one with no change to
/// the callers; the staff policies and the <see cref="AuthenticatedActor"/> shape are unchanged.
/// This adapter lives in the Api layer because establishing a cookie session and reading the
/// current principal are inseparable from the ASP.NET Core sign-in and <c>HttpContext</c>
/// frameworks (coding-standards §3). Expected failures (unknown account, wrong password, locked
/// out) are returned as a uniform <see cref="ErrorCode.Validation"/> result so the surface does not
/// disclose which accounts exist.
/// </para>
/// </remarks>
public sealed class CookieStaffAuthenticator : IStaffAuthenticator
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Creates the authenticator with its Identity and request dependencies.</summary>
    /// <param name="signInManager">The Identity sign-in manager that establishes the cookie session.</param>
    /// <param name="userManager">The Identity user manager used to resolve the account and its role.</param>
    /// <param name="httpContextAccessor">Accessor for the current request's authenticated principal.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public CookieStaffAuthenticator(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        _signInManager = signInManager;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<Result<AuthenticatedActor>> SignInAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return InvalidCredentials();
        }

        AppUser? user = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (user is null)
        {
            return InvalidCredentials();
        }

        Microsoft.AspNetCore.Identity.SignInResult signInResult = await _signInManager
            .PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (!signInResult.Succeeded)
        {
            return InvalidCredentials();
        }

        Role role = await ResolveRoleAsync(user).ConfigureAwait(false);
        return Result.Success(new AuthenticatedActor(user.Id, user.Email ?? string.Empty, role));
    }

    /// <inheritdoc />
    public Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _signInManager.SignOutAsync();
    }

    /// <inheritdoc />
    public AuthenticatedActor? GetCurrentActor()
    {
        ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
        {
            return null;
        }

        string? userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out Guid userId) || userId == Guid.Empty)
        {
            return null;
        }

        string email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? string.Empty;

        Role role = principal.IsInRole(RoleNames.Owner) ? Role.Owner : Role.Staff;
        return new AuthenticatedActor(userId, email, role);
    }

    private async Task<Role> ResolveRoleAsync(AppUser user)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        // Owner authority supersedes Staff when an account somehow carries both.
        return roles.Contains(RoleNames.Owner, StringComparer.Ordinal) ? Role.Owner : Role.Staff;
    }

    private static Result<AuthenticatedActor> InvalidCredentials() =>
        Result.Failure<AuthenticatedActor>(ErrorCode.Validation, "The email or password is incorrect.");
}
