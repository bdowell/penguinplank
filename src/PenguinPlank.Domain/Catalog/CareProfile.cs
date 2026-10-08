using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A reusable care-guidance identity associated with product variants. A care profile owns
/// one or more immutable <see cref="CareProfileVersion"/> records.
/// </summary>
/// <remarks>
/// The care profile is the stable identity; editing care guidance never mutates the profile's
/// existing versions but appends a new <see cref="CareProfileVersion"/> (requirements 2.3, 2.5
/// / design invariant 10). The profile itself derives from <see cref="Entity"/> as low-churn
/// reference data (its name and active flag may be edited, but it needs no ETag concurrency
/// token). The append-only versioning rule is a domain policy added by a later task.
/// </remarks>
public class CareProfile : Entity
{
    /// <summary>The care profile name (for example, "Cutting board care"). Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the profile is active and offered for new variant associations.</summary>
    public bool ActiveFlag { get; set; } = true;
}
