using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Abstractions.Concurrency;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Infrastructure.Persistence.Concurrency;

/// <summary>
/// The EF Core implementation of <see cref="IConcurrentUpdateExecutor"/>. It forces a conditional
/// <c>UPDATE ... WHERE rowversion = @ifMatch</c> and translates a lost race into a stable
/// <see cref="ErrorCode.StaleVersion"/> failure, never overwriting newer state
/// (requirements A4 §6.5/§6.6; design invariant 14).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="VersionedEntity.RowVersion"/> column is mapped as an EF Core concurrency token
/// (<c>IsRowVersion</c> in the base entity convention), so EF already appends
/// <c>WHERE rowversion = @original</c> to the generated <c>UPDATE</c>. The subtlety is which value
/// EF treats as the "original": by default it uses whatever version EF loaded, which may be newer
/// than the one the client actually saw. To honor the client's <c>If-Match</c> token precisely,
/// this executor overwrites the tracked entry's <em>original</em> row-version value with the
/// supplied token before saving, so the UPDATE's <c>WHERE</c> clause filters on exactly the
/// version the client intended to replace.
/// </para>
/// <para>
/// When the stored row has already advanced, the conditional UPDATE affects zero rows and EF Core
/// raises <see cref="DbUpdateConcurrencyException"/>. That is an <em>expected</em> business outcome
/// here — a concurrent edit — so it is caught at this defined boundary and translated to a typed
/// <see cref="Result"/> failure rather than propagating as an unexpected exception
/// (coding-standards §6). The caller receives <see cref="ErrorCode.StaleVersion"/> and surfaces a
/// 412 refresh prompt; the newer value in the database is left intact.
/// </para>
/// <para>
/// This type captures the scoped <see cref="PenguinPlankDbContext"/>, so it is registered
/// <b>scoped</b> in the composition root — never as a singleton that would capture a scoped
/// context (coding-standards §2).
/// </para>
/// </remarks>
public sealed class EfConcurrentUpdateExecutor : IConcurrentUpdateExecutor
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>
    /// Creates the executor over the scoped application <see cref="PenguinPlankDbContext"/>.
    /// </summary>
    /// <param name="dbContext">The scoped EF Core context tracking the aggregate to update.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="dbContext"/> is <see langword="null"/>.
    /// </exception>
    public EfConcurrentUpdateExecutor(PenguinPlankDbContext dbContext)
    {
        System.ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        VersionedEntity entity,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(entity);
        System.ArgumentNullException.ThrowIfNull(expectedRowVersion);

        // Pin the UPDATE's WHERE clause to the exact version the client presented via If-Match,
        // regardless of the version EF currently has tracked. This guarantees the conditional
        // "UPDATE ... WHERE rowversion = @ifMatch" semantics even if the entity was reloaded.
        _dbContext
            .Entry(entity)
            .Property(e => e.RowVersion)
            .OriginalValue = expectedRowVersion;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row moved on since the client read it: zero rows matched the If-Match version.
            // Translate this expected concurrency conflict to a stable StaleVersion failure
            // (-> HTTP 412 refresh prompt) rather than overwriting the newer state.
            return Result.Failure(
                ErrorCode.StaleVersion,
                "The record was changed by someone else since you loaded it. "
                    + "Refresh to see the latest version and reapply your changes.");
        }
    }
}
