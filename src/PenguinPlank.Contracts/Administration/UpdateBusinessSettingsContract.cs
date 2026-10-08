namespace PenguinPlank.Contracts.Administration;

/// <summary>
/// The versioned request body for an Owner edit of the business settings (requirement A2 §5.3; A4
/// §6.5, §6.6).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO (dependency rule; coding-standards §3). The concurrency token is sent
/// in the HTTP <c>If-Match</c> header, not in this body, so a stale edit is refused with a
/// <c>412</c> rather than silently overwriting newer settings.
/// </remarks>
public sealed record UpdateBusinessSettingsContract
{
    /// <summary>The IANA business timezone (for example "America/Los_Angeles").</summary>
    public required string Timezone { get; init; }

    /// <summary>The ISO 4217 currency code for monetary values (for example "USD").</summary>
    public required string Currency { get; init; }

    /// <summary>The default labor rate applied where a specific rate is absent.</summary>
    public required decimal DefaultLaborRate { get; init; }

    /// <summary>The default overhead rate applied where a specific rate is absent.</summary>
    public required decimal DefaultOverheadRate { get; init; }

    /// <summary>The default unit of measure for dimensions (for example "in").</summary>
    public required string DefaultDimensionUnit { get; init; }

    /// <summary>The image upload size-limit warning threshold in bytes.</summary>
    public required long ImageSizeLimitBytes { get; init; }

    /// <summary>The video upload size-limit warning threshold in bytes.</summary>
    public required long VideoSizeLimitBytes { get; init; }
}
