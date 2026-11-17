namespace PenguinPlank.Contracts.Integrations;

/// <summary>
/// The versioned request body an Owner submits to confirm one external-to-internal mapping
/// (requirement R11 / 3.4, 3.5).
/// </summary>
public sealed record CreateMappingContract
{
    /// <summary>The owning integration connection.</summary>
    public required Guid ConnectionId { get; init; }

    /// <summary>The external platform. Must match the connection's platform.</summary>
    public required string Platform { get; init; }

    /// <summary>The external account or shop. Must match the connection's account.</summary>
    public required string Account { get; init; }

    /// <summary>The external resource identifier.</summary>
    public required string ExternalId { get; init; }

    /// <summary>The kind of internal entity mapped to, for example "ProductVariant".</summary>
    public required string OwnerType { get; init; }

    /// <summary>The internal record identifier.</summary>
    public required Guid InternalId { get; init; }
}
