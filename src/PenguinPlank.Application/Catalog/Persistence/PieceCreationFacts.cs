using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The existing-state facts a create-piece use case needs about the owning <c>ProductVariant</c>
/// and the catalog's existing piece codes before it may append a new <c>ProductPiece</c>.
/// </summary>
/// <remarks>
/// <para>
/// A use case loads this snapshot through <see cref="ICatalogPieceStore"/> and feeds it to the pure
/// decisions — the variant active flag to <c>ArchivedMasterDataPolicy.CanTransact</c>, the existing
/// codes to <c>PieceCodePolicy.ValidateUnique</c>, and the resolved care versions to
/// <c>CareVersionResolver.ResolveForProduction</c> — before persisting (coding-standards §1). It
/// carries ordinary values and the pure Domain <see cref="CareProfileVersion"/> entities (a Domain
/// type, not a persistence type), so no EF/persistence type crosses the boundary (coding-standards
/// §2, §3). This is an immutable value with no I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record PieceCreationFacts
{
    /// <summary>Whether the owning variant is active; an archived variant rejects new pieces (requirement 1.8).</summary>
    public required bool VariantIsActive { get; init; }

    /// <summary>The owning variant's tracking mode; pieces are individuals under a serialized variant.</summary>
    public required TrackingMode VariantTrackingMode { get; init; }

    /// <summary>The care profile the owning variant references, or <see langword="null"/> when none.</summary>
    public Guid? CareProfileId { get; init; }

    /// <summary>
    /// The versions of the variant's care profile, used to resolve the version in effect at the
    /// piece's production time (requirement 2.4). Empty when the variant references no profile or it
    /// has no versions. Never <see langword="null"/>.
    /// </summary>
    public required IReadOnlyList<CareProfileVersion> CareProfileVersions { get; init; }

    /// <summary>
    /// The business piece codes already held by existing pieces, for the uniqueness check
    /// (requirement 1.7). Never <see langword="null"/>; may be empty.
    /// </summary>
    public required IReadOnlyCollection<string?> ExistingPieceCodes { get; init; }
}
