using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The Infrastructure implementation of the coarse <see cref="ICareProfileStore"/> append-only
/// care-versioning boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Delegate the mutation, read directly.</b> Appending a version is append-only and carries
/// business rules (the archived guard and the pure next-version computation), so this store
/// delegates <see cref="AddVersionAsync"/> to the single-responsibility
/// <see cref="AddCareVersionUseCase"/> (task 8.2) rather than duplicating those rules — a care edit
/// never mutates an existing version, only inserts the next one (requirements 2.3, 2.5). The two
/// read operations have no business rule, so they run parameterized no-tracking EF queries directly
/// and map to the Application <see cref="CareProfileVersionView"/> read model; no EF entity crosses
/// the boundary (coding-standards §1, §2, §3). A produced piece always resolves the exact version
/// recorded at its production time (requirement 2.4), which
/// <see cref="ResolveVersionForPieceAsync"/> returns.
/// </para>
/// <para>
/// It captures the scoped <see cref="PenguinPlankDbContext"/> and is registered scoped. The read
/// operations return <see langword="null"/> or an empty list for an absent record rather than
/// throwing (coding-standards §6).
/// </para>
/// </remarks>
public sealed class CareProfileStore : ICareProfileStore
{
    private readonly PenguinPlankDbContext _dbContext;
    private readonly AddCareVersionUseCase _addCareVersionUseCase;

    /// <summary>Creates the store over the scoped database context and the add-version use case.</summary>
    /// <param name="dbContext">The scoped application database context used for the read operations.</param>
    /// <param name="addCareVersionUseCase">The append-version orchestration this store delegates to.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public CareProfileStore(PenguinPlankDbContext dbContext, AddCareVersionUseCase addCareVersionUseCase)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(addCareVersionUseCase);

        _dbContext = dbContext;
        _addCareVersionUseCase = addCareVersionUseCase;
    }

    /// <inheritdoc />
    public Task<Result<CareProfileVersionView>> AddVersionAsync(
        AddCareVersionRequest request,
        ActorContext actor,
        CancellationToken cancellationToken) =>
        _addCareVersionUseCase.ExecuteAsync(request, actor, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CareProfileVersionView>> GetVersionsAsync(
        Guid careProfileId,
        CancellationToken cancellationToken)
    {
        List<CareProfileVersionView> versions = await _dbContext.CareProfileVersions
            .AsNoTracking()
            .Where(version => version.CareProfileId == careProfileId)
            .OrderBy(version => version.VersionNumber)
            .Select(version => new CareProfileVersionView
            {
                CareProfileVersionId = version.Id,
                CareProfileId = version.CareProfileId,
                VersionNumber = version.VersionNumber,
                Guidance = version.Guidance,
                CreatedAtUtc = version.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return versions;
    }

    /// <inheritdoc />
    public async Task<CareProfileVersionView?> ResolveVersionForPieceAsync(
        Guid pieceId,
        CancellationToken cancellationToken)
    {
        // The piece preserves the exact version resolved at production time (requirement 2.4): read
        // its stored CareProfileVersionId, then project that immutable version. A piece whose
        // variant referenced no care profile has no preserved version — a legitimate null.
        Guid? careProfileVersionId = await _dbContext.ProductPieces
            .AsNoTracking()
            .Where(piece => piece.Id == pieceId)
            .Select(piece => piece.CareProfileVersionId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (careProfileVersionId is not Guid versionId)
        {
            return null;
        }

        CareProfileVersionView? view = await _dbContext.CareProfileVersions
            .AsNoTracking()
            .Where(version => version.Id == versionId)
            .Select(version => new CareProfileVersionView
            {
                CareProfileVersionId = version.Id,
                CareProfileId = version.CareProfileId,
                VersionNumber = version.VersionNumber,
                Guidance = version.Guidance,
                CreatedAtUtc = version.CreatedAtUtc,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return view;
    }
}
