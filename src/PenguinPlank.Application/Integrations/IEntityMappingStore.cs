using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The persistence boundary for external-to-internal mappings (requirement R11 / 3.4, 3.5).
/// A mapping preserves platform, account, and external identifiers under a unique association
/// to one internal record. SKU matches are only ever returned as suggestions.
/// </summary>
public interface IEntityMappingStore
{
    /// <summary>
    /// Records an authoritative mapping. A mapping on an already-used
    /// (platform, account, external id) triple is rejected and the existing one is left unchanged.
    /// </summary>
    /// <param name="request">The mapping to record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The stored mapping, or a failure: <see cref="ErrorCode.Validation"/> for invalid input or an
    /// unknown or mismatched connection, <see cref="ErrorCode.MappingConflict"/> for a duplicate triple.
    /// </returns>
    Task<Result<EntityMappingRecord>> MapAsync(
        EntityMappingRequest request,
        CancellationToken cancellationToken);

    /// <summary>Finds the mapping for a triple, or null when none exists.</summary>
    /// <param name="platform">The external platform.</param>
    /// <param name="account">The external account or shop.</param>
    /// <param name="externalId">The external resource identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The mapping, or null.</returns>
    Task<EntityMappingRecord?> FindAsync(
        string platform,
        string account,
        string externalId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Detects SKU matches between external candidates and internal variants and returns them as
    /// reviewable suggestions. Nothing is written, and externals that are already mapped are skipped.
    /// </summary>
    /// <param name="connectionId">The connection the candidates came from.</param>
    /// <param name="candidates">The external variants with their SKUs.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The suggestions, or a validation failure when the connection does not exist.</returns>
    Task<Result<IReadOnlyList<MappingSuggestion>>> SuggestSkuMatchesAsync(
        Guid connectionId,
        IReadOnlyList<ExternalSkuCandidate> candidates,
        CancellationToken cancellationToken);
}
