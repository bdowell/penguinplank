using PenguinPlank.Application.Abstractions;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The persistence boundary for <b>append-only</b> care-profile versioning: add a new immutable
/// version, read a profile's versions, and resolve the version in effect for a produced piece
/// (requirements R10 §2.3, §2.4, §2.5).
/// </summary>
/// <remarks>
/// <para>
/// This is a narrow, responsibility-named boundary interface defined in Application and
/// implemented in Infrastructure over EF Core (coding-standards §2, §3). Its contract enforces the
/// append-only rule: <see cref="AddVersionAsync"/> inserts a new version and never updates or
/// deletes an existing one, so prior versions are preserved verbatim (requirements 2.3, 2.5). A
/// produced piece always resolves the version that was current at its production time
/// (requirement 2.4), which <see cref="ResolveVersionForPieceAsync"/> exposes.
/// </para>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/> and returns read models
/// rather than EF entities so no persistence type leaks across the boundary. <see cref="AddVersionAsync"/>
/// returns a typed <see cref="Result{T}"/> for expected business failures (for example an unknown or
/// archived profile); the read operations return the version(s) or <see langword="null"/> when none
/// match and do not throw for an absent record (coding-standards §6). Time comes from an injected
/// <see cref="System.TimeProvider"/> in the implementation.
/// </para>
/// </remarks>
public interface ICareProfileStore
{
    /// <summary>
    /// Appends a new immutable version to a care profile and returns it. Never modifies an existing
    /// version (requirements 2.3, 2.5).
    /// </summary>
    /// <param name="request">The owning profile and the new guidance text.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success carrying the created <see cref="CareProfileVersionView"/>, or a typed failure.</returns>
    Task<Result<CareProfileVersionView>> AddVersionAsync(AddCareVersionRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>
    /// Lists every version of a care profile in version-number order. Returns an empty list when the
    /// profile has no versions.
    /// </summary>
    /// <param name="careProfileId">The owning care profile.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The profile's versions, oldest first; empty when none exist.</returns>
    Task<IReadOnlyList<CareProfileVersionView>> GetVersionsAsync(Guid careProfileId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the care-profile version a produced piece preserves — the version in effect at the
    /// piece's production time (requirement 2.4).
    /// </summary>
    /// <param name="pieceId">The produced piece whose preserved care version is requested.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The preserved <see cref="CareProfileVersionView"/>, or <see langword="null"/> when the piece
    /// has no resolved care version (for example, its variant references no care profile).
    /// </returns>
    Task<CareProfileVersionView?> ResolveVersionForPieceAsync(Guid pieceId, CancellationToken cancellationToken);
}
