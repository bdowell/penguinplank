namespace PenguinPlank.Application.Abstractions;

/// <summary>
/// Documents and anchors the project-wide time convention. Time-dependent application
/// behavior obtains the current instant from an injected
/// <see cref="System.TimeProvider"/>; it never reads <see cref="System.DateTime.UtcNow"/>
/// or <see cref="System.DateTime.Now"/> directly inside a business decision.
/// </summary>
/// <remarks>
/// <para>
/// <b>Standard (requirement A8 §10.1; coding-standards §2).</b> The BCL
/// <see cref="System.TimeProvider"/> is the single source of the current instant for
/// application services. Register one instance in the API composition root
/// (<c>TimeProvider.System</c> in production) and inject it through constructors. Tests
/// substitute a controllable provider (for example
/// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>) so time-dependent behavior
/// is deterministic and fast — no sleeping, no real clock.
/// </para>
/// <para>
/// <b>IClock decision.</b> Earlier task text referred to an <c>IClock</c> abstraction.
/// Phase A deliberately standardizes on the BCL <see cref="System.TimeProvider"/> instead
/// of introducing a custom clock interface: coding-standards §2 states "Use TimeProvider
/// for time-dependent application behavior." <see cref="System.TimeProvider"/> already
/// supplies a controllable, injectable clock (and timers), so a bespoke <c>IClock</c>
/// would add an interchangeable abstraction with no additional capability
/// (coding-standards §2: do not add interfaces where a boundary type already suffices).
/// No <c>IClock</c> type is defined.
/// </para>
/// <para>
/// <b>Forbidden in business decisions.</b> Direct use of
/// <see cref="System.DateTime.UtcNow"/> / <see cref="System.DateTime.Now"/> (and
/// <see cref="System.DateTimeOffset.UtcNow"/> / <see cref="System.DateTimeOffset.Now"/>)
/// is not permitted inside domain or application business logic. Prefer passing the
/// resolved instant into pure domain functions so calculations stay deterministic and
/// directly testable (coding-standards §1, §2). Instant timestamps are UTC.
/// </para>
/// <para>
/// This type carries no behavior or state; it exists to give the convention a single,
/// discoverable home referenced by code review and onboarding.
/// </para>
/// </remarks>
public static class TimeConvention
{
    /// <summary>
    /// Returns the current UTC instant from the supplied <see cref="System.TimeProvider"/>.
    /// Use this (or <c>timeProvider.GetUtcNow()</c> directly) rather than
    /// <see cref="System.DateTimeOffset.UtcNow"/> so the clock stays injectable and testable.
    /// </summary>
    /// <param name="timeProvider">The injected time source.</param>
    /// <returns>The current instant as a UTC <see cref="System.DateTimeOffset"/>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public static DateTimeOffset UtcNow(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return timeProvider.GetUtcNow();
    }
}
