namespace PenguinPlank.Web.Shell;

/// <summary>The result of a sign-in attempt, with a caller-safe message on failure.</summary>
/// <param name="Succeeded">Whether the session was established.</param>
/// <param name="Message">A message suitable for display when sign-in failed; otherwise <see langword="null"/>.</param>
public sealed record SignInOutcome(bool Succeeded, string? Message)
{
    /// <summary>Gets the successful outcome.</summary>
    public static SignInOutcome Success { get; } = new(true, null);

    /// <summary>Creates a failed outcome.</summary>
    /// <param name="message">The message to show the user.</param>
    /// <returns>A failed outcome.</returns>
    public static SignInOutcome Failure(string message) => new(false, message);
}
