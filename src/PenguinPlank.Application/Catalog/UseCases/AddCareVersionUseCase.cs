using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog.UseCases;

/// <summary>
/// Appends a new immutable version to a <c>CareProfile</c>. Editing care guidance never mutates an
/// existing version; it creates the next one, so prior versions are preserved verbatim
/// (requirements R10 §2.3, §2.5).
/// </summary>
/// <remarks>
/// <para>
/// The use case loads the profile's facts (active flag and current highest version number) through
/// the narrow <see cref="ICareProfileVersionStore"/>, applies the archived guard with
/// <see cref="ArchivedMasterDataPolicy"/>, computes the next version number with the pure
/// <see cref="CareVersionResolver.NextVersionNumber(int)"/>, and appends on acceptance. Because the
/// store only ever inserts a new row for a version, no existing version is modified or deleted — the
/// append-only rule holds at this layer and is reinforced by the insert-only persistence mapping
/// (requirements 2.3, 2.5). On rejection it returns a typed failure without appending
/// (coding-standards §1).
/// </para>
/// </remarks>
public sealed class AddCareVersionUseCase
{
    private readonly ICareProfileVersionStore _careProfileVersionStore;
    private readonly IIdentifierGenerator _identifierGenerator;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="careProfileVersionStore">The care-profile persistence boundary used to load facts and append.</param>
    /// <param name="identifierGenerator">The generator for the new version's identifier.</param>
    /// <param name="auditSink">The audit boundary a successful append records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the creation instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public AddCareVersionUseCase(
        ICareProfileVersionStore careProfileVersionStore,
        IIdentifierGenerator identifierGenerator,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(careProfileVersionStore);
        ArgumentNullException.ThrowIfNull(identifierGenerator);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _careProfileVersionStore = careProfileVersionStore;
        _identifierGenerator = identifierGenerator;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the add-care-version orchestration.</summary>
    /// <param name="request">The owning profile and the new guidance text.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the appended <see cref="CareProfileVersionView"/>, or a typed failure (an
    /// archived or unknown profile).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<CareProfileVersionView>> ExecuteAsync(
        AddCareVersionRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        // 1. Load the profile's facts.
        CareProfileFacts? facts = await _careProfileVersionStore
            .GetCareProfileFactsAsync(request.CareProfileId, cancellationToken)
            .ConfigureAwait(false);

        if (facts is null)
        {
            return Result.Failure<CareProfileVersionView>(ErrorCode.Validation, "The care profile does not exist.");
        }

        // 2. Appending a version is a new transaction against the profile: reject if archived.
        Result archivedResult = ArchivedMasterDataPolicy.CanTransact(facts.IsActive);
        if (archivedResult.IsFailure)
        {
            return Result.Failure<CareProfileVersionView>(archivedResult.Error);
        }

        // 3. Compute the next version number with the pure resolver (append-only).
        int nextVersionNumber = CareVersionResolver.NextVersionNumber(facts.CurrentMaxVersionNumber);

        // 4. Accepted: append the new immutable version and audit it.
        Guid versionId = _identifierGenerator.NewId();
        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();

        var record = new NewCareVersionRecord
        {
            CareProfileVersionId = versionId,
            CareProfileId = request.CareProfileId,
            VersionNumber = nextVersionNumber,
            Guidance = request.Guidance,
            CreatedAtUtc = createdAtUtc,
        };

        CareProfileVersionView view = await _careProfileVersionStore
            .AppendVersionAsync(record, cancellationToken)
            .ConfigureAwait(false);

        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(nameof(CareProfileVersion), versionId, AuditOperation.Created),
                    actor,
                    createdAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(view);
    }
}
