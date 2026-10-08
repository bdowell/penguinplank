using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Abstractions.Concurrency;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.IdentityAdministration;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.IdentityAdministration;

/// <summary>
/// The EF Core implementation of <see cref="IBusinessSettingsStore"/> over the
/// <see cref="PenguinPlankDbContext"/>. It reads the single business-settings row and applies an
/// Owner-decided update under optimistic concurrency (requirement A2 §5.3; A4 §6.5, §6.6).
/// </summary>
/// <remarks>
/// <para>
/// The adapter makes no business or authorization decision: the update use case decides and the
/// settings endpoints are gated Owner-only; this store only reads and writes rows
/// (coding-standards §1, §3). It projects to the Application <see cref="BusinessSettingsView"/> and
/// never returns an EF entity, <c>DbContext</c>, or <c>IQueryable</c> across the boundary. The
/// update honors the client's <c>If-Match</c> token through the shared
/// <see cref="IConcurrentUpdateExecutor"/> so a stale write fails with
/// <see cref="ErrorCode.StaleVersion"/> rather than overwriting newer settings. It captures the
/// scoped context and is registered scoped.
/// </para>
/// </remarks>
public sealed class EfBusinessSettingsStore : IBusinessSettingsStore
{
    private readonly PenguinPlankDbContext _dbContext;
    private readonly IConcurrentUpdateExecutor _concurrentUpdateExecutor;

    /// <summary>Creates the store over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <param name="concurrentUpdateExecutor">The optimistic-concurrency executor honoring <c>If-Match</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public EfBusinessSettingsStore(
        PenguinPlankDbContext dbContext,
        IConcurrentUpdateExecutor concurrentUpdateExecutor)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(concurrentUpdateExecutor);

        _dbContext = dbContext;
        _concurrentUpdateExecutor = concurrentUpdateExecutor;
    }

    /// <inheritdoc />
    public async Task<BusinessSettingsView?> GetAsync(CancellationToken cancellationToken)
    {
        BusinessSettings? settings = await _dbContext.BusinessSettings
            .AsNoTracking()
            .OrderBy(record => record.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return settings is null ? null : ToView(settings);
    }

    /// <inheritdoc />
    public async Task<Result<BusinessSettingsView>> UpdateAsync(
        UpdateBusinessSettingsRequest request,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ETag.TryParse(request.ExpectedVersion, out byte[] expectedRowVersion))
        {
            return Result.Failure<BusinessSettingsView>(
                ErrorCode.StaleVersion,
                "The settings concurrency token is missing or malformed; refresh and retry.");
        }

        BusinessSettings? settings = await _dbContext.BusinessSettings
            .OrderBy(record => record.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            return Result.Failure<BusinessSettingsView>(
                ErrorCode.Validation,
                "The business settings have not been initialized.");
        }

        settings.Timezone = request.Timezone;
        settings.Currency = request.Currency;
        settings.DefaultLaborRate = request.DefaultLaborRate;
        settings.DefaultOverheadRate = request.DefaultOverheadRate;
        settings.DefaultDimensionUnit = request.DefaultDimensionUnit;
        settings.ImageSizeLimitBytes = request.ImageSizeLimitBytes;
        settings.VideoSizeLimitBytes = request.VideoSizeLimitBytes;
        settings.UpdatedAtUtc = updatedAtUtc;

        Result updateResult = await _concurrentUpdateExecutor
            .ExecuteAsync(settings, expectedRowVersion, cancellationToken)
            .ConfigureAwait(false);

        if (updateResult.IsFailure)
        {
            return Result.Failure<BusinessSettingsView>(updateResult.Error);
        }

        return Result.Success(ToView(settings));
    }

    private static BusinessSettingsView ToView(BusinessSettings settings) => new()
    {
        Timezone = settings.Timezone,
        Currency = settings.Currency,
        DefaultLaborRate = settings.DefaultLaborRate,
        DefaultOverheadRate = settings.DefaultOverheadRate,
        DefaultDimensionUnit = settings.DefaultDimensionUnit,
        ImageSizeLimitBytes = settings.ImageSizeLimitBytes,
        VideoSizeLimitBytes = settings.VideoSizeLimitBytes,
        ETagToken = ETag.Encode(settings.RowVersion),
    };
}
