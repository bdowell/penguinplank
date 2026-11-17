namespace PenguinPlank.Contracts.Integrations;

/// <summary>The versioned transport shape for a stored external-to-internal mapping (requirement R11 / 3.4).</summary>
public sealed record MappingResponse
{
    /// <summary>The mapping identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The owning integration connection.</summary>
    public required Guid ConnectionId { get; init; }

    /// <summary>The external platform.</summary>
    public required string Platform { get; init; }

    /// <summary>The external account or shop.</summary>
    public required string Account { get; init; }

    /// <summary>The external resource identifier.</summary>
    public required string ExternalId { get; init; }

    /// <summary>The kind of internal entity mapped to.</summary>
    public required string OwnerType { get; init; }

    /// <summary>The internal record identifier.</summary>
    public required Guid InternalId { get; init; }
}
