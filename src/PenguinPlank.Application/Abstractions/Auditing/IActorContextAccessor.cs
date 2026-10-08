namespace PenguinPlank.Application.Abstractions.Auditing;

/// <summary>
/// Supplies the <see cref="ActorContext"/> for the current unit of work so a persistence-level
/// component — notably the audit <c>SaveChanges</c> interceptor — can attribute a business
/// mutation to its actor without taking a dependency on <c>HttpContext</c> or ambient web state
/// (coding-standards §2, §3).
/// </summary>
/// <remarks>
/// <para>
/// The accessor is a <b>scoped</b> boundary: one instance exists per request scope and per
/// background-worker job scope, mirroring the lifetime of the scoped
/// <c>PenguinPlankDbContext</c> whose <c>SaveChanges</c> the interceptor observes. The API
/// boundary resolves the authenticated principal and calls <see cref="SetCurrent"/> with the
/// per-request <see cref="ActorContext"/> before any mutation is saved; the background worker calls
/// <see cref="SetCurrent"/> with <see cref="ActorContext.SystemWorker"/> so worker-initiated mutations are
/// audited with the recorded system identity (requirement A7 §9.6).
/// </para>
/// <para>
/// Keeping actor resolution behind this narrow, named interface (coding-standards §2) means the
/// interceptor depends on an explicit, controllable substitute in tests rather than reaching into
/// framework state, and the audit-entry construction stays a pure function of the described change
/// set plus the actor and timestamp (so the audit-coverage property can be asserted without a
/// database).
/// </para>
/// </remarks>
public interface IActorContextAccessor
{
    /// <summary>
    /// The actor for the current scope, or <see langword="null"/> if no actor has been established
    /// yet. A mutation attempted before an actor is set is a programming error at the boundary: the
    /// API and the worker must set the actor before saving.
    /// </summary>
    ActorContext? Current { get; }

    /// <summary>
    /// Records the actor for the current scope. Called once at the API boundary (per request) or by
    /// the background worker (<see cref="ActorContext.SystemWorker"/>) before any business mutation
    /// is saved.
    /// </summary>
    /// <param name="actor">The actor responsible for mutations in this scope.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="actor"/> is <see langword="null"/>.
    /// </exception>
    void SetCurrent(ActorContext actor);
}
