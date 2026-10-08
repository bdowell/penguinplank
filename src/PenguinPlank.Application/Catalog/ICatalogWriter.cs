using PenguinPlank.Application.Abstractions;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The persistence boundary for catalog <b>mutations</b>: creating and editing products,
/// variants, and pieces; transitioning publication; archiving and unarchiving master data; and
/// setting wood composition (requirements R01 §1.1, §1.6, §1.8–§1.13, R10 §2.6, §2.7).
/// </summary>
/// <remarks>
/// <para>
/// This is a narrow, responsibility-named boundary interface defined in Application and
/// implemented in Infrastructure over EF Core (coding-standards §2, §3). It is deliberately not a
/// generic repository wrapping every <c>DbSet</c>; it exposes the specific catalog write
/// operations the Phase A use cases need so each can be exercised against a controllable substitute
/// (coding-standards §6).
/// </para>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/>, accepts an explicit
/// <see cref="ActorContext"/> resolved at the API boundary (so neither Domain nor Application
/// touches <c>HttpContext</c>), and returns a typed <see cref="Result"/>/<see cref="Result{T}"/>
/// for expected business failures — a duplicate SKU or piece code, an invalid dimension or
/// proportion, a transaction against archived master data, or a stale concurrency token — rather
/// than throwing; unexpected faults throw (coding-standards §6). Time comes from an injected
/// <see cref="System.TimeProvider"/> in the implementation, not from
/// <see cref="System.DateTime.UtcNow"/> inside a business decision.
/// </para>
/// </remarks>
public interface ICatalogWriter
{
    /// <summary>Creates a <c>Product</c> family record in the default <c>Draft</c> publication state.</summary>
    /// <param name="request">The product creation inputs.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success carrying the new product's identifier, or a typed failure.</returns>
    Task<Result<Guid>> CreateProductAsync(CreateProductRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>Updates an existing <c>Product</c>, rejecting a stale concurrency token (requirements 6.5, 6.6).</summary>
    /// <param name="request">The product update inputs, including the expected version.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a typed failure (for example a stale version or an archived record).</returns>
    Task<Result> UpdateProductAsync(UpdateProductRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a <c>ProductVariant</c> (the design's variant draft), rejecting a duplicate SKU or
    /// barcode, an invalid dimension, or an invalid wood proportion (requirements 1.1–1.5).
    /// </summary>
    /// <param name="request">The variant creation inputs.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success carrying the new variant's identifier, or a typed failure.</returns>
    Task<Result<Guid>> CreateVariantAsync(CreateVariantRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>Updates an existing <c>ProductVariant</c>, rejecting a stale concurrency token (requirements 6.5, 6.6).</summary>
    /// <param name="request">The variant update inputs, including the expected version.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a typed failure (for example a stale version, a duplicate barcode, or an archived record).</returns>
    Task<Result> UpdateVariantAsync(UpdateVariantRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a <c>ProductPiece</c> under a serialized variant, rejecting a duplicate piece code
    /// and recording the care-profile version in effect at production time (requirements 1.6, 1.7,
    /// 2.4). The piece's public story starts in <c>Draft</c> (requirements 1.12, 2.6, 2.7).
    /// </summary>
    /// <param name="request">The piece creation inputs.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success carrying the new piece's identifier, or a typed failure.</returns>
    Task<Result<Guid>> CreatePieceAsync(CreatePieceRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>
    /// Transitions a record to the public-approved state. Publication is explicit and never
    /// automatic (requirements 1.12, 2.6, 2.7); a stale concurrency token is rejected
    /// (requirements 6.5, 6.6).
    /// </summary>
    /// <param name="request">The target record and its expected version.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a typed failure.</returns>
    Task<Result> PublishAsync(PublicationTransitionRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>Withdraws a record from public exposure back to <c>Draft</c>, rejecting a stale concurrency token.</summary>
    /// <param name="request">The target record and its expected version.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a typed failure.</returns>
    Task<Result> WithdrawAsync(PublicationTransitionRequest request, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>
    /// Archives a product, variant, or piece. An archived record stays readable but rejects new
    /// transactions (requirement 1.8).
    /// </summary>
    /// <param name="target">The record to archive.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a typed failure.</returns>
    Task<Result> ArchiveAsync(CatalogRef target, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>Reactivates a previously archived product, variant, or piece.</summary>
    /// <param name="target">The record to unarchive.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a typed failure.</returns>
    Task<Result> UnarchiveAsync(CatalogRef target, ActorContext actor, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the wood composition of a variant or piece, rejecting any proportion outside the
    /// 0–100 range (requirements 1.5, 2.2).
    /// </summary>
    /// <param name="request">The target record and the full desired set of composition components.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a typed failure.</returns>
    Task<Result> SetWoodCompositionAsync(SetWoodCompositionRequest request, ActorContext actor, CancellationToken cancellationToken);
}
