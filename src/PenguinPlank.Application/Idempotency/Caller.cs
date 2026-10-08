using PenguinPlank.Application.Abstractions;

namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The authenticated caller that issued a mutating command, persisted with the idempotency key
/// so a stored record is attributable to who produced it (requirement A4 §6.2).
/// </summary>
/// <remarks>
/// This is an immutable value object (coding-standards §1). The caller identity originates from
/// the <see cref="ActorContext"/> resolved at the API boundary; the domain and application
/// logic never touch <c>HttpContext</c>. In Phase A the stored <see cref="Id"/> is a plain GUID
/// with no foreign key to the Identity tables, matching <c>IdempotencyRecord.CallerId</c>.
/// </remarks>
public readonly record struct Caller
{
    /// <summary>Creates a caller from an explicit identifier.</summary>
    /// <param name="id">The caller's identifier; must be non-empty.</param>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public Caller(System.Guid id)
    {
        if (id == System.Guid.Empty)
        {
            throw new System.ArgumentException("A caller requires a non-empty identifier.", nameof(id));
        }

        Id = id;
    }

    /// <summary>The caller's identifier.</summary>
    public System.Guid Id { get; }

    /// <summary>
    /// Creates a caller from the actor resolved at the API boundary, using its
    /// <see cref="ActorContext.UserId"/> (which is the system-worker sentinel for background jobs).
    /// </summary>
    /// <param name="actor">The actor context for the command.</param>
    /// <returns>A caller identifying <paramref name="actor"/>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
    public static Caller FromActor(ActorContext actor)
    {
        System.ArgumentNullException.ThrowIfNull(actor);
        return new Caller(actor.UserId);
    }
}
