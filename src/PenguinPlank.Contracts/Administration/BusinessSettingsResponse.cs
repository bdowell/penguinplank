namespace PenguinPlank.Contracts.Administration;

/// <summary>
/// The versioned transport shape returned for the single-row business settings (requirement A2
/// §5.3).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO with no persistence entity or EF type (dependency rule;
/// coding-standards §3) — the API maps the Application <c>BusinessSettingsView</c> read model into
/// this shape. Monetary and rate values are explicit decimals and the <see cref="Currency"/> and
/// <see cref="DefaultDimensionUnit"/> are returned explicitly so a client never infers units
/// (requirement A8 §10.3). <see cref="ETag"/> echoes the aggregate's opaque concurrency token so a
/// subsequent edit presents it as an <c>If-Match</c> value (requirement A4 §6.5, §6.6).
/// </remarks>
public sealed record BusinessSettingsResponse
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

    /// <summary>The opaque concurrency token to echo as <c>If-Match</c> on a subsequent edit.</summary>
    public required string ETag { get; init; }
}
