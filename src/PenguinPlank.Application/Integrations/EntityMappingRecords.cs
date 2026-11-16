namespace PenguinPlank.Application.Integrations;

/// <summary>A request to map one external resource to one internal record.</summary>
/// <param name="ConnectionId">The owning integration connection.</param>
/// <param name="Platform">The external platform. Must match the connection's platform.</param>
/// <param name="Account">The external account or shop. Must match the connection's account.</param>
/// <param name="ExternalId">The external resource identifier.</param>
/// <param name="OwnerType">The kind of internal entity mapped to (for example "ProductVariant").</param>
/// <param name="InternalId">The internal record identifier.</param>
public sealed record EntityMappingRequest(
    Guid ConnectionId,
    string Platform,
    string Account,
    string ExternalId,
    string OwnerType,
    Guid InternalId);

/// <summary>A stored external-to-internal mapping.</summary>
/// <param name="Id">The mapping identifier.</param>
/// <param name="ConnectionId">The owning integration connection.</param>
/// <param name="Platform">The external platform.</param>
/// <param name="Account">The external account or shop.</param>
/// <param name="ExternalId">The external resource identifier.</param>
/// <param name="OwnerType">The kind of internal entity mapped to.</param>
/// <param name="InternalId">The internal record identifier.</param>
public sealed record EntityMappingRecord(
    Guid Id,
    Guid ConnectionId,
    string Platform,
    string Account,
    string ExternalId,
    string OwnerType,
    Guid InternalId);

/// <summary>An external variant that carries a SKU, offered for SKU-match suggestion.</summary>
/// <param name="ExternalId">The external resource identifier.</param>
/// <param name="Sku">The SKU reported by the external platform.</param>
public sealed record ExternalSkuCandidate(string ExternalId, string Sku);

/// <summary>An internal variant identity used when matching SKUs.</summary>
/// <param name="VariantId">The internal ProductVariant identifier.</param>
/// <param name="Sku">The internal SKU.</param>
public sealed record InternalVariantSku(Guid VariantId, string Sku);

/// <summary>
/// A detected SKU match presented for review. It is a suggestion only: it is never stored as an
/// <see cref="EntityMappingRecord"/> until a reviewer confirms it through
/// <see cref="IEntityMappingStore.MapAsync"/> (requirement R11 / 3.5).
/// </summary>
/// <param name="ExternalId">The external resource identifier.</param>
/// <param name="Sku">The SKU the two sides share (the internal spelling).</param>
/// <param name="VariantId">The internal ProductVariant identifier suggested.</param>
public sealed record MappingSuggestion(string ExternalId, string Sku, Guid VariantId);
