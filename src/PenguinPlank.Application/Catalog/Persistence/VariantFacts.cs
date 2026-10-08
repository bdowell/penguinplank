using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The existing-state facts about a <c>ProductVariant</c> that a mutating use case needs to apply
/// the pure domain policies before persisting an edit.
/// </summary>
/// <remarks>
/// <para>
/// A use case loads this snapshot through <see cref="ICatalogVariantStore"/> and feeds its fields
/// to the pure decisions — the active flag to <c>ArchivedMasterDataPolicy.CanTransact</c>, the
/// current tracking mode plus <see cref="HasStockHistory"/> to
/// <c>TrackingModeChangePolicy.CanChange</c> — then persists under the loaded
/// <see cref="RowVersion"/> for optimistic concurrency (coding-standards §1). It carries ordinary
/// values only; no EF entity crosses the boundary (coding-standards §2, §3). This is an immutable
/// value with no I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record VariantFacts
{
    /// <summary>The variant's stable identifier.</summary>
    public required Guid VariantId { get; init; }

    /// <summary>The owning product's identifier.</summary>
    public required Guid ProductId { get; init; }

    /// <summary>Whether the variant is active; an archived variant rejects new transactions (requirement 1.8).</summary>
    public required bool IsActive { get; init; }

    /// <summary>The variant's current stock tracking mode.</summary>
    public required TrackingMode TrackingMode { get; init; }

    /// <summary>
    /// Whether the variant already has recorded stock history, which locks a tracking-mode change
    /// absent a designed migration (requirement 1.13).
    /// </summary>
    public required bool HasStockHistory { get; init; }

    /// <summary>The variant's current optimistic-concurrency token (its <c>rowversion</c>).</summary>
    public required byte[] RowVersion { get; init; }
}
