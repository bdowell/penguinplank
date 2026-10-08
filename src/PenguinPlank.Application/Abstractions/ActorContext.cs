namespace PenguinPlank.Application.Abstractions;

/// <summary>
/// The authenticated actor responsible for a business mutation: an Owner, a Staff
/// user, or the system/background-worker identity.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ActorContext"/> is an immutable value object. It is constructed once at
/// the API boundary (where the authenticated principal is known) and passed explicitly
/// into every use case that performs or audits a business mutation. Domain and
/// application logic therefore never depend on <c>HttpContext</c>, a service locator, or
/// ambient state (coding-standards §2, §3; requirements A2 §5.12).
/// </para>
/// <para>
/// Background jobs do not have an interactive principal. They run through the same
/// application services using a recorded system actor obtained from
/// <see cref="SystemWorker"/>, so worker-initiated mutations are audited with a stable,
/// recognizable identity (requirement A7 §9.6).
/// </para>
/// <para>
/// The type is a pure value: it holds ordinary data, performs no I/O, and is directly
/// testable without starting the application (coding-standards §1).
/// </para>
/// </remarks>
public sealed record ActorContext
{
    /// <summary>
    /// The stable identifier recorded for the system/background-worker actor. It is a
    /// fixed, non-empty sentinel so that worker-initiated mutations are consistently
    /// attributable in audit records.
    /// </summary>
    public static readonly Guid SystemWorkerId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Creates an actor context for an authenticated staff account (Owner or Staff).
    /// </summary>
    /// <param name="userId">The authenticated user's identifier; must be non-empty.</param>
    /// <param name="role">The authorization role carried by the account.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is empty.</exception>
    public ActorContext(Guid userId, Role role)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user actor requires a non-empty user id.", nameof(userId));
        }

        UserId = userId;
        Role = role;
        IsSystemWorker = false;
    }

    private ActorContext(Guid userId, Role role, bool isSystemWorker)
    {
        UserId = userId;
        Role = role;
        IsSystemWorker = isSystemWorker;
    }

    /// <summary>The identifier of the acting user, or <see cref="SystemWorkerId"/> for the system worker.</summary>
    public Guid UserId { get; }

    /// <summary>The authorization role of the actor. The system worker acts with <see cref="Role.Owner"/> authority.</summary>
    public Role Role { get; }

    /// <summary>
    /// <see langword="true"/> when this context represents the background-worker system
    /// actor rather than an interactive user.
    /// </summary>
    public bool IsSystemWorker { get; }

    /// <summary>
    /// Creates the recorded system/background-worker actor. Background jobs use this to
    /// run through application services with an audited identity (requirement A7 §9.6).
    /// </summary>
    /// <returns>An immutable system-worker <see cref="ActorContext"/>.</returns>
    public static ActorContext SystemWorker()
    {
        return new ActorContext(SystemWorkerId, Role.Owner, isSystemWorker: true);
    }
}
