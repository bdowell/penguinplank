namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Pure SKU-match detection. It only produces <see cref="MappingSuggestion"/> values; it never
/// creates a mapping (requirement R11 / 3.5).
/// </summary>
public static class SkuMatchSuggester
{
    /// <summary>
    /// Suggests an internal variant for each unmapped external candidate whose SKU matches exactly
    /// one internal SKU (ignoring case and surrounding whitespace). Ambiguous or blank SKUs yield
    /// no suggestion.
    /// </summary>
    /// <param name="candidates">The external candidates.</param>
    /// <param name="variants">The internal variants to match against.</param>
    /// <param name="alreadyMappedExternalIds">External ids that already have a mapping.</param>
    /// <returns>The suggestions, in candidate order.</returns>
    public static IReadOnlyList<MappingSuggestion> Suggest(
        IEnumerable<ExternalSkuCandidate> candidates,
        IEnumerable<InternalVariantSku> variants,
        IReadOnlySet<string> alreadyMappedExternalIds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(alreadyMappedExternalIds);

        Dictionary<string, List<InternalVariantSku>> bySku = variants
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Sku))
            .GroupBy(variant => variant.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var suggestions = new List<MappingSuggestion>();
        foreach (ExternalSkuCandidate candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Sku)
                || alreadyMappedExternalIds.Contains(candidate.ExternalId)
                || !bySku.TryGetValue(candidate.Sku.Trim(), out List<InternalVariantSku>? matches)
                || matches.Count != 1)
            {
                continue;
            }

            suggestions.Add(new MappingSuggestion(candidate.ExternalId, matches[0].Sku, matches[0].VariantId));
        }

        return suggestions;
    }
}
