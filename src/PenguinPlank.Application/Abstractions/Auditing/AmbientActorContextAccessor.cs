namespace PenguinPlank.Application.Abstractions.Auditing;

/// <summary>
/// The default scoped <see cref="IActorContextAccessor"/>: a simple holder for the actor
/// established for the current request or background-worker scope.
/// </summary>
/// <remarks>
/// <para>
/// This holds ordinary state and performs no I/O, so it is directly testable (coding-standards
/// §1). It is registered <b>scoped</b> in the composition root so that each request scope and each
/// background-worker job scope gets its own instance, matching the scoped
/// <c>PenguinPlankDbContext</c> and the audit interceptor that read from it. It deliberately does
/// not default to any actor: a mutation saved before the boundary set one indicates missing
/// boundary wiring and must surface rather than be silently attributed.
/// </para>
/// </remarks>
public sealed class AmbientActorContextAccessor : IActorContextAccessor
{
    /// <inheritdoc />
    public ActorContext? Current { get; private set; }

    /// <inheritdoc />
    public void SetCurrent(ActorContext actor)
    {
        System.ArgumentNullException.ThrowIfNull(actor);
        Current = actor;
    }
}
