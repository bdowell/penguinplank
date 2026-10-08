namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// A decided request to update the single-row business settings under optimistic concurrency
/// (requirement A2 §5.3; A4 §6.5, §6.6).
/// </summary>
/// <remarks>
/// An immutable value carried from the Owner-only settings endpoint into the update use case.
/// <see cref="ExpectedVersion"/> is the client's <c>If-Match</c> token, so a stale edit is refused
/// with <see cref="PenguinPlank.Domain.Common.ErrorCode.StaleVersion"/> rather than silently
/// overwriting newer settings (requirement A4 §6.6).
/// </remarks>
public sealed record UpdateBusinessSettingsRequest
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

    /// <summary>The client's <c>If-Match</c> concurrency token for the optimistic-concurrency check.</summary>
    public required string ExpectedVersion { get; init; }
}
