namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// Reads the single-row business settings (requirement A2 §5.3). This is the read side the thin
/// Owner-only <c>/settings</c> endpoint maps onto; it keeps the lookup orchestration out of the API
/// layer (coding-standards §3).
/// </summary>
/// <remarks>
/// A thin pass-through over <see cref="IBusinessSettingsStore.GetAsync"/>, returning the Application
/// <see cref="BusinessSettingsView"/> read model (never an EF entity) or <see langword="null"/> when
/// the settings have not been seeded. Authorization is enforced at the API boundary (Owner-only).
/// </remarks>
public sealed class GetBusinessSettingsUseCase
{
    private readonly IBusinessSettingsStore _settingsStore;

    /// <summary>Creates the use case with its injected boundary.</summary>
    /// <param name="settingsStore">The business-settings persistence boundary.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settingsStore"/> is <see langword="null"/>.</exception>
    public GetBusinessSettingsUseCase(IBusinessSettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);

        _settingsStore = settingsStore;
    }

    /// <summary>Reads the single business-settings record.</summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The settings view, or <see langword="null"/> when the record has not been seeded.</returns>
    public Task<BusinessSettingsView?> ExecuteAsync(CancellationToken cancellationToken) =>
        _settingsStore.GetAsync(cancellationToken);
}
