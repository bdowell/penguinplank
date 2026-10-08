using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog.UseCases;

/// <summary>
/// Records a <c>ChannelListing</c> — a stored reference to a variant's listing on an external
/// channel — requiring the channel, external id, and status, and refusing a listing against
/// archived master data (requirements R01 §1.8, §1.9, §1.10).
/// </summary>
/// <remarks>
/// <para>
/// A channel listing is a <b>reference record only</b> in Phase A: the use case records the
/// listing but performs <b>no channel synchronization</b> while no connected channel is enabled
/// under the integration foundation (requirement 1.10). It validates the three required fields,
/// loads the listed variant's active flag through the narrow <see cref="IChannelListingStore"/>,
/// applies the archived guard with <see cref="ArchivedMasterDataPolicy"/>, and persists only on
/// acceptance. On rejection it returns a typed failure without persisting (coding-standards §1).
/// </para>
/// </remarks>
public sealed class RecordChannelListingUseCase
{
    private readonly IChannelListingStore _listingStore;
    private readonly IIdentifierGenerator _identifierGenerator;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="listingStore">The channel-listing persistence boundary used to confirm the variant and insert.</param>
    /// <param name="identifierGenerator">The generator for the new listing's identifier.</param>
    /// <param name="auditSink">The audit boundary a successful record writes a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the record instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public RecordChannelListingUseCase(
        IChannelListingStore listingStore,
        IIdentifierGenerator identifierGenerator,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(listingStore);
        ArgumentNullException.ThrowIfNull(identifierGenerator);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _listingStore = listingStore;
        _identifierGenerator = identifierGenerator;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the record-channel-listing orchestration.</summary>
    /// <param name="request">The channel-listing inputs.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the new listing's identifier, or a typed failure (a missing required
    /// field, an archived variant, or an unknown variant).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<Guid>> ExecuteAsync(
        RecordChannelListingRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        // 1. The channel, external id, and status are required (requirement 1.9).
        Result requiredFieldsResult = ValidateRequiredFields(request);
        if (requiredFieldsResult.IsFailure)
        {
            return Result.Failure<Guid>(requiredFieldsResult.Error);
        }

        // 2. Load the listed variant's active flag and apply the archived guard.
        bool? variantActive = await _listingStore
            .GetVariantActiveAsync(request.VariantId, cancellationToken)
            .ConfigureAwait(false);

        if (variantActive is null)
        {
            return Result.Failure<Guid>(ErrorCode.Validation, "The listed variant does not exist.");
        }

        Result archivedResult = ArchivedMasterDataPolicy.CanTransact(variantActive.Value);
        if (archivedResult.IsFailure)
        {
            return Result.Failure<Guid>(archivedResult.Error);
        }

        // 3. Accepted: persist the reference record (no synchronization) and audit it.
        Guid listingId = _identifierGenerator.NewId();
        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();

        var record = new NewChannelListingRecord
        {
            ChannelListingId = listingId,
            VariantId = request.VariantId,
            Channel = request.Channel,
            ExternalId = request.ExternalId,
            Status = request.Status,
            CreatedAtUtc = createdAtUtc,
            Url = request.Url,
            ReadinessState = request.ReadinessState,
        };

        await _listingStore.InsertListingAsync(record, cancellationToken).ConfigureAwait(false);

        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(nameof(ChannelListing), listingId, AuditOperation.Created),
                    actor,
                    createdAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(listingId);
    }

    private static Result ValidateRequiredFields(RecordChannelListingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            return Result.Failure(ErrorCode.Validation, "A channel listing requires a channel.");
        }

        if (string.IsNullOrWhiteSpace(request.ExternalId))
        {
            return Result.Failure(ErrorCode.Validation, "A channel listing requires an external id.");
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return Result.Failure(ErrorCode.Validation, "A channel listing requires a status.");
        }

        return Result.Success();
    }
}
