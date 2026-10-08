using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="ICareProfileVersionStore"/> over the
/// <see cref="PenguinPlankDbContext"/>. It loads a care profile's facts and appends the next
/// immutable version.
/// </summary>
/// <remarks>
/// <para>
/// Care-profile versioning is append-only (requirements 2.3, 2.5): <see cref="AppendVersionAsync"/>
/// inserts a brand-new row and never updates or deletes an existing version, which the
/// insert-only persistence mapping (<c>CareProfileVersionConfiguration</c>) reinforces at the row
/// level. The add-care-version use case computes the next version number with the pure
/// <c>CareVersionResolver</c> and the archived guard; the adapter holds no business rule
/// (coding-standards §1, §3). It returns the persisted version as the Application
/// <see cref="CareProfileVersionView"/> read model so no EF entity crosses the boundary
/// (coding-standards §2, §3). The store captures the scoped context and is registered scoped.
/// </para>
/// </remarks>
public sealed class EfCareProfileVersionStore : ICareProfileVersionStore
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfCareProfileVersionStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CareProfileFacts?> GetCareProfileFactsAsync(Guid careProfileId, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.CareProfiles
            .AsNoTracking()
            .Where(candidate => candidate.Id == careProfileId)
            .Select(candidate => new { candidate.ActiveFlag })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return null;
        }

        // The current highest version number, or 0 when the profile has no versions yet. The
        // nullable projection distinguishes "no versions" from a real zero without a second query.
        int? currentMax = await _dbContext.CareProfileVersions
            .AsNoTracking()
            .Where(version => version.CareProfileId == careProfileId)
            .Select(version => (int?)version.VersionNumber)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CareProfileFacts
        {
            IsActive = profile.ActiveFlag,
            CurrentMaxVersionNumber = currentMax ?? 0,
        };
    }

    /// <inheritdoc />
    public async Task<CareProfileVersionView> AppendVersionAsync(
        NewCareVersionRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var version = new CareProfileVersion
        {
            Id = record.CareProfileVersionId,
            CareProfileId = record.CareProfileId,
            VersionNumber = record.VersionNumber,
            Guidance = record.Guidance,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.CreatedAtUtc,
        };

        _dbContext.CareProfileVersions.Add(version);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CareProfileVersionView
        {
            CareProfileVersionId = version.Id,
            CareProfileId = version.CareProfileId,
            VersionNumber = version.VersionNumber,
            Guidance = version.Guidance,
            CreatedAtUtc = version.CreatedAtUtc,
        };
    }
}
