using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Common;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Domain.Common;

namespace UnitTests.IdentityAdministration.Administration;

/// <summary>
/// Hand-written controllable substitutes for the boundaries the administration use cases depend on
/// — the business-settings store, the staff-user store, and the audit reader (task 9.3).
/// </summary>
/// <remarks>
/// Each fake is a focused substitute over exactly the interface its use case depends on
/// (coding-standards §2, §7): no EF <c>DbSet</c>/<c>IQueryable</c> is mocked, and no live SQL,
/// network, file system, or real clock is involved. The fakes record what the use case decided (the
/// applied update, the created account, the forwarded query) so a test asserts observable outcomes
/// rather than private call order, and each honours an already-cancelled token so the cancellation
/// cases are deterministic.
/// </remarks>
internal static class AdministrationFakes
{
    /// <summary>A fixed, recognizable instant the tests assert the use cases stamp from the clock.</summary>
    public static readonly DateTimeOffset FixedNow = new(2024, 8, 15, 10, 0, 0, TimeSpan.Zero);
}

/// <summary>Controllable substitute for <see cref="IBusinessSettingsStore"/>.</summary>
internal sealed class FakeBusinessSettingsStore : IBusinessSettingsStore
{
    public BusinessSettingsView? GetResult { get; set; }

    public Result<BusinessSettingsView>? UpdateResult { get; set; }

    /// <summary>The update the use case applied, with the stamped instant, or <see langword="null"/> when none was.</summary>
    public (UpdateBusinessSettingsRequest Request, DateTimeOffset UpdatedAtUtc)? AppliedUpdate { get; private set; }

    public Task<BusinessSettingsView?> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetResult);
    }

    public Task<Result<BusinessSettingsView>> UpdateAsync(
        UpdateBusinessSettingsRequest request,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AppliedUpdate = (request, updatedAtUtc);
        return Task.FromResult(UpdateResult ?? throw new InvalidOperationException("Arrange UpdateResult."));
    }
}

/// <summary>Controllable substitute for <see cref="IStaffUserStore"/>.</summary>
internal sealed class FakeStaffUserStore : IStaffUserStore
{
    public IReadOnlyList<StaffUserView> ListResult { get; set; } = [];

    public Result<StaffUserView>? CreateResult { get; set; }

    /// <summary>The create the use case forwarded, or <see langword="null"/> when none was.</summary>
    public (CreateStaffUserRequest Request, ActorContext Actor, DateTimeOffset AuditedAtUtc)? Created { get; private set; }

    public Task<IReadOnlyList<StaffUserView>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ListResult);
    }

    public Task<Result<StaffUserView>> CreateAsync(
        CreateStaffUserRequest request,
        ActorContext actor,
        DateTimeOffset auditedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Created = (request, actor, auditedAtUtc);
        return Task.FromResult(CreateResult ?? throw new InvalidOperationException("Arrange CreateResult."));
    }
}

/// <summary>Controllable substitute for <see cref="IAuditReader"/>.</summary>
internal sealed class FakeAuditReader : IAuditReader
{
    public Page<AuditEntryView> ListResult { get; set; } = Page.Empty<AuditEntryView>(PageRequest.Default);

    /// <summary>The query the use case forwarded, or <see langword="null"/> when none was.</summary>
    public AuditQuery? ForwardedQuery { get; private set; }

    public Task<Page<AuditEntryView>> ListAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ForwardedQuery = query;
        return Task.FromResult(ListResult);
    }
}
