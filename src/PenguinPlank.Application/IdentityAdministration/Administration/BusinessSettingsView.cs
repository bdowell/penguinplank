namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// The Application read model for the single-row business settings (requirement A2 §5.3): timezone,
/// currency, default rates, dimension unit, media size thresholds, and the opaque concurrency token.
/// </summary>
/// <remarks>
/// A persistence-agnostic view mapped from the <c>BusinessSettings</c> entity; no EF entity,
/// <c>DbContext</c>, or <c>IQueryable</c> crosses the boundary (coding-standards §3). The
/// <see cref="ETagToken"/> exposes the aggregate's <c>rowversion</c> so an edit can require
/// <c>If-Match</c> and a stale write is refused rather than silently overwriting (requirement A4
/// §6.5, §6.6).
/// </remarks>
public sealed record BusinessSettingsView
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

    /// <summary>The opaque concurrency token exposed as the HTTP <c>ETag</c> for optimistic concurrency.</summary>
    public required string ETagToken { get; init; }
}
