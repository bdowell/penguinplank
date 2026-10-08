using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PenguinPlank.Application.IdentityAdministration;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.IdentityAdministration;

namespace UnitTests.IdentityAdministration;

/// <summary>
/// Use-case-level behavior of the one-time Owner bootstrap (requirement A2 Â§5.8): it creates the
/// Owner exactly once, is safe to re-run (idempotent), and refuses a missing/placeholder password
/// without ever creating an account.
/// </summary>
/// <remarks>
/// <para>
/// <b>Substitute approach.</b> The real <see cref="OwnerBootstrapper"/> is exercised over a real
/// ASP.NET Core Identity <see cref="UserManager{TUser}"/> and <see cref="RoleManager{TRole}"/>
/// backed by hand-written in-memory stores (<see cref="InMemoryUserStore"/> /
/// <see cref="InMemoryRoleStore"/>). This runs the production orchestration â€” role existence check,
/// idempotent no-op, pure refusal policy, Identity password-policy validation, hashing, and role
/// assignment â€” against deterministic, in-memory state. It proves <em>orchestration</em>, which is
/// what Â§5.8 asks for; relational guarantees (unique indexes, transactions) are covered by the
/// IntegrationTests suite against real SQL Server (coding-standards Â§7). No database, network,
/// file system, or real clock is involved.
/// </para>
/// </remarks>
public sealed class OwnerBootstrapperTests
{
    private const string ValidOperatorPassword = "Zr7!qvW2m#Lx";
    private const string OwnerEmail = "owner@penguinplank.test";

    /// <summary>
    /// A controllable harness wiring the real OwnerBootstrapper over real Identity managers backed
    /// by in-memory stores, so a sequence of bootstrap calls shares one store (needed to assert
    /// idempotency across two calls).
    /// </summary>
    private sealed class BootstrapHarness : IDisposable
    {
        private readonly InMemoryUserStore _userStore;

        public BootstrapHarness(string? email, string? initialPassword)
        {
            _userStore = new InMemoryUserStore();
            var roleStore = new InMemoryRoleStore();

            UserManager = BuildUserManager(_userStore);
            RoleManager = BuildRoleManager(roleStore);

            var options = Options.Create(new OwnerBootstrapOptions
            {
                Email = email,
                InitialPassword = initialPassword,
            });

            Bootstrapper = new OwnerBootstrapper(
                UserManager,
                RoleManager,
                options,
                NullLogger<OwnerBootstrapper>.Instance);
        }

        public OwnerBootstrapper Bootstrapper { get; }

        public UserManager<AppUser> UserManager { get; }

        public RoleManager<IdentityRole<Guid>> RoleManager { get; }

        public int StoredUserCount => _userStore.UserCount;

        public void Dispose()
        {
            // UserManager/RoleManager dispose the stores they were given; dispose the managers
            // (and the directly held user store) so the analyzer-tracked disposables are released.
            UserManager.Dispose();
            RoleManager.Dispose();
            _userStore.Dispose();
        }

        private static UserManager<AppUser> BuildUserManager(IUserStore<AppUser> store)
        {
            var identityOptions = Options.Create(new IdentityOptions());

            // A password policy with real requirements so the bootstrap's reliance on the Identity
            // policy (not just the pure refusal rule) is genuinely exercised.
            identityOptions.Value.Password.RequiredLength = 8;
            identityOptions.Value.Password.RequireDigit = true;
            identityOptions.Value.Password.RequireUppercase = true;
            identityOptions.Value.Password.RequireLowercase = true;
            identityOptions.Value.Password.RequireNonAlphanumeric = true;

            var hasher = new PasswordHasher<AppUser>();
            var userValidators = new List<IUserValidator<AppUser>> { new UserValidator<AppUser>() };
            var passwordValidators = new List<IPasswordValidator<AppUser>> { new PasswordValidator<AppUser>() };

            return new UserManager<AppUser>(
                store,
                identityOptions,
                hasher,
                userValidators,
                passwordValidators,
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                services: null!,
                NullLogger<UserManager<AppUser>>.Instance);
        }

        private static RoleManager<IdentityRole<Guid>> BuildRoleManager(IRoleStore<IdentityRole<Guid>> store)
        {
            var roleValidators = new List<IRoleValidator<IdentityRole<Guid>>>
            {
                new RoleValidator<IdentityRole<Guid>>(),
            };

            return new RoleManager<IdentityRole<Guid>>(
                store,
                roleValidators,
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                NullLogger<RoleManager<IdentityRole<Guid>>>.Instance);
        }
    }

    [Fact]
    public async Task EnsureOwnerAsync_WhenNoOwnerExists_CreatesOwnerWithRolesAndReportsCreatedAsync()
    {
        using var harness = new BootstrapHarness(OwnerEmail, ValidOperatorPassword);

        Result<OwnerBootstrapOutcome> result =
            await harness.Bootstrapper.EnsureOwnerAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OwnerBootstrapOutcome.Created, result.Value);

        // The Owner and Staff roles were ensured.
        Assert.True(await harness.RoleManager.RoleExistsAsync(RoleNames.Owner));
        Assert.True(await harness.RoleManager.RoleExistsAsync(RoleNames.Staff));

        // Exactly one user exists and it is an Owner with a hashed (non-plaintext) password.
        Assert.Equal(1, harness.StoredUserCount);
        IList<AppUser> owners = await harness.UserManager.GetUsersInRoleAsync(RoleNames.Owner);
        AppUser owner = Assert.Single(owners);
        Assert.Equal(OwnerEmail, owner.Email);
        Assert.NotNull(owner.PasswordHash);
        Assert.NotEqual(ValidOperatorPassword, owner.PasswordHash);
    }

    [Fact]
    public async Task EnsureOwnerAsync_RunTwice_IsIdempotentAndCreatesNoDuplicateOwnerAsync()
    {
        using var harness = new BootstrapHarness(OwnerEmail, ValidOperatorPassword);

        Result<OwnerBootstrapOutcome> first =
            await harness.Bootstrapper.EnsureOwnerAsync(CancellationToken.None);
        Result<OwnerBootstrapOutcome> second =
            await harness.Bootstrapper.EnsureOwnerAsync(CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(OwnerBootstrapOutcome.Created, first.Value);

        // The second run is a safe no-op: it reports AlreadyExists and adds no second user.
        Assert.True(second.IsSuccess);
        Assert.Equal(OwnerBootstrapOutcome.AlreadyExists, second.Value);
        Assert.Equal(1, harness.StoredUserCount);

        IList<AppUser> owners = await harness.UserManager.GetUsersInRoleAsync(RoleNames.Owner);
        Assert.Single(owners);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ChangeMe")]
    [InlineData("password")]
    public async Task EnsureOwnerAsync_WithRefusedOrMissingPassword_FailsValidationAndCreatesNoUserAsync(
        string? refusedPassword)
    {
        using var harness = new BootstrapHarness(OwnerEmail, refusedPassword);

        Result<OwnerBootstrapOutcome> result =
            await harness.Bootstrapper.EnsureOwnerAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);

        // No account was created from a refused/placeholder password: there is no baked-in default.
        Assert.Equal(0, harness.StoredUserCount);
        IList<AppUser> owners = await harness.UserManager.GetUsersInRoleAsync(RoleNames.Owner);
        Assert.Empty(owners);
    }

    [Fact]
    public async Task EnsureOwnerAsync_WithMissingEmail_FailsValidationAndCreatesNoUserAsync()
    {
        using var harness = new BootstrapHarness(email: "   ", initialPassword: ValidOperatorPassword);

        Result<OwnerBootstrapOutcome> result =
            await harness.Bootstrapper.EnsureOwnerAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Equal(0, harness.StoredUserCount);
    }

    [Fact]
    public async Task EnsureOwnerAsync_WhenPasswordFailsIdentityPolicy_FailsValidationAndCreatesNoUserAsync()
    {
        // "abcdefg1" passes the pure refusal rule (not a placeholder) but fails the configured
        // Identity policy (no uppercase, no non-alphanumeric), proving the bootstrap also honors
        // the account policy, not just the pure refusal check.
        using var harness = new BootstrapHarness(OwnerEmail, "abcdefg1");

        Result<OwnerBootstrapOutcome> result =
            await harness.Bootstrapper.EnsureOwnerAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Equal(0, harness.StoredUserCount);
    }
}
