namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The narrow persistence boundary an add-care-version use case depends on to <b>load a care
/// profile's facts</b> (its active state and current highest version number) and to <b>append the
/// next immutable version</b>.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists so the add-care-version use case (task 8.2) can be orchestrated and
/// unit-tested against a controllable substitute (task 8.4) while Infrastructure provides the EF
/// Core implementation (task 8.3). It is a focused, responsibility-named boundary (coding-standards
/// §2, §6). Care-profile versioning is append-only: <see cref="AppendVersionAsync"/> inserts a new
/// row and never updates or deletes an existing one, so prior versions are preserved verbatim
/// (requirements 2.3, 2.5). The business decisions — the archived guard and the next version number
/// from the pure <c>CareVersionResolver</c> — live in the use case, never in the adapter
/// (coding-standards §1, §3).
/// </para>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/>; the fact-loading read
/// returns <see langword="null"/> for an absent profile and does not throw for absence. No EF entity
/// crosses the boundary — only the Application snapshot/record types (coding-standards §2, §3).
/// </para>
/// </remarks>
public interface ICareProfileVersionStore
{
    /// <summary>Loads a care profile's facts, or <see langword="null"/> when no profile matches.</summary>
    /// <param name="careProfileId">The profile whose facts are requested.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The profile's facts, or <see langword="null"/> when no profile matches.</returns>
    Task<CareProfileFacts?> GetCareProfileFactsAsync(Guid careProfileId, CancellationToken cancellationToken);

    /// <summary>Appends a new immutable version exactly as the use case decided it; never modifies an existing version.</summary>
    /// <param name="record">The fully decided version values to persist.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The persisted version as a read model.</returns>
    Task<CareProfileVersionView> AppendVersionAsync(NewCareVersionRecord record, CancellationToken cancellationToken);
}
