namespace PenguinPlank.Domain.Common;

/// <summary>
/// The base type for <b>mutable aggregates</b> that require optimistic-concurrency
/// protection. It extends <see cref="Entity"/> with a concurrency token.
/// </summary>
/// <remarks>
/// <para>
/// Mutable aggregates (for example a product variant, a stock balance, or an integration
/// connection) must never be silently overwritten by a stale edit. SQL Server provides
/// an automatically maintained <c>rowversion</c> column for this; EF Core maps it as a
/// concurrency token so a conditional <c>UPDATE ... WHERE rowversion = @ifMatch</c>
/// affects zero rows when the row has moved on, surfacing a 412 refresh prompt at the API
/// (design "ETag / If-Match optimistic concurrency"; invariant 6 / requirement 6.5).
/// </para>
/// <para>
/// <b>Kept EF-free.</b> <see cref="RowVersion"/> is a plain <see cref="byte"/> array on a
/// domain POCO — the Domain layer declares the <em>shape</em>, not the persistence
/// behavior. Infrastructure configures it as a <c>rowversion</c> concurrency token in the
/// base entity convention; no <c>[Timestamp]</c> or other EF attribute leaks into Domain
/// (coding-standards §3). The database owns the value, so the property is initialized to
/// an empty array rather than left null.
/// </para>
/// <para>
/// Append-only / immutable records (which are inserted once and never updated) do not need
/// a concurrency token and derive from <see cref="Entity"/> instead.
/// </para>
/// </remarks>
public abstract class VersionedEntity : Entity
{
    /// <summary>
    /// The SQL <c>rowversion</c> concurrency token. The database assigns and advances this
    /// value on every write; application code treats it as opaque and surfaces it as an
    /// HTTP ETag. Configured as a concurrency token in Infrastructure.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays — rowversion is an opaque EF concurrency token mapped from a SQL byte array.
    public byte[] RowVersion { get; set; } = [];
#pragma warning restore CA1819
}
