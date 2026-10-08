namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// Lists the staff accounts for the Owner-only <c>/users</c> endpoint (requirement A2 §5.9, §5.10).
/// </summary>
/// <remarks>
/// A thin pass-through over <see cref="IStaffUserStore.ListAsync"/>, returning role-safe
/// <see cref="StaffUserView"/> read models that carry no credential. Authorization is enforced at
/// the API boundary (Owner-only).
/// </remarks>
public sealed class ListStaffUsersUseCase
{
    private readonly IStaffUserStore _staffUserStore;

    /// <summary>Creates the use case with its injected boundary.</summary>
    /// <param name="staffUserStore">The staff-account administration boundary.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="staffUserStore"/> is <see langword="null"/>.</exception>
    public ListStaffUsersUseCase(IStaffUserStore staffUserStore)
    {
        ArgumentNullException.ThrowIfNull(staffUserStore);

        _staffUserStore = staffUserStore;
    }

    /// <summary>Lists every staff account.</summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The role-safe views of all staff accounts.</returns>
    public Task<IReadOnlyList<StaffUserView>> ExecuteAsync(CancellationToken cancellationToken) =>
        _staffUserStore.ListAsync(cancellationToken);
}
