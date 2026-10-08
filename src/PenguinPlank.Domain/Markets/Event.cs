using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Markets;

/// <summary>
/// A market/show event: its internal and public descriptions, venue, timezone, dates,
/// status, setup/load-out, and published state.
/// </summary>
/// <remarks>
/// Designed-only in Phase A (design Section 6); no services exercise it. It is a mutable
/// aggregate. Public and internal descriptions are stored in <b>separate columns</b> so a
/// public projection never leaks internal content (design invariant 11). Date fields carry
/// date-only local intent (A8). <see cref="StartDate"/> must be on or before
/// <see cref="EndDate"/> (design invariant 3); the check is a later-phase domain policy.
/// </remarks>
/// <remarks>
/// <b>CA1716 suppression rationale.</b> The design (Section 6) names this market entity
/// <c>Event</c>, which is the business's domain vocabulary (coding-standards §4 requires using
/// domain vocabulary consistently). CA1716 flags <c>Event</c> as a reserved keyword in some
/// CLR languages (notably Visual Basic); this product is C#-only and never consumed from VB, so
/// the cross-language concern does not apply. Renaming would diverge from the authoritative
/// specification vocabulary, so the warning is suppressed on this type with this rationale
/// rather than the design being altered (coding-standards §8).
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Design Section 6 names this market entity 'Event' as domain vocabulary; the product is C#-only and never consumed from VB, so the cross-language keyword concern does not apply.")]
public class Event : VersionedEntity
{
    /// <summary>The event name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The internal-only description (never exposed publicly).</summary>
    public string? InternalDescription { get; set; }

    /// <summary>The public-safe description.</summary>
    public string? PublicDescription { get; set; }

    /// <summary>The venue name/address.</summary>
    public string? Venue { get; set; }

    /// <summary>The IANA timezone the event's local dates are interpreted in.</summary>
    public string? Timezone { get; set; }

    /// <summary>The event start date (date-only local intent; see A8).</summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>The event end date (date-only local intent; see A8).</summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>The event status (a free-form operational status label in Phase A).</summary>
    public string? Status { get; set; }

    /// <summary>Setup notes/instructions.</summary>
    public string? SetupNotes { get; set; }

    /// <summary>Load-out notes/instructions.</summary>
    public string? LoadOutNotes { get; set; }

    /// <summary>The publication state of the event's public content. Defaults to Draft.</summary>
    public PublicationState PublicationState { get; set; } = PublicationState.Draft;
}
