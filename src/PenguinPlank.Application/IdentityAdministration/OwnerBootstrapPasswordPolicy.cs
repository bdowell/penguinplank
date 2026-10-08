namespace PenguinPlank.Application.IdentityAdministration;

/// <summary>
/// The pure rule that decides whether an operator-supplied initial Owner password is
/// acceptable to <em>attempt</em> account creation with — specifically, that it is present
/// and is not an obvious default/placeholder (requirement A2 §5.8).
/// </summary>
/// <remarks>
/// <para>
/// This is a deterministic business decision with explicit inputs and no dependencies, so it
/// is tested directly with ordinary values (coding-standards §1). It intentionally does
/// <b>not</b> evaluate password <em>strength</em>: strength is enforced against the configured
/// ASP.NET Core Identity password policy by the bootstrapper (a boundary that owns the policy),
/// so the two checks compose without this pure function depending on Identity.
/// </para>
/// <para>
/// The refusal exists to guarantee there is no baked-in production password: an empty, blank,
/// or placeholder value is rejected rather than silently replaced with a hardcoded default. The
/// placeholder list below catches the common "forgot to set the secret" values; it is a
/// defensive convenience, not a substitute for the operator supplying a real secret.
/// </para>
/// </remarks>
public static class OwnerBootstrapPasswordPolicy
{
    /// <summary>
    /// Well-known placeholder values that indicate the operator did not supply a real secret.
    /// Matched case-insensitively after trimming. This is deliberately conservative — it rejects
    /// obvious non-secrets without trying to be a password-strength check.
    /// </summary>
    private static readonly string[] s_knownPlaceholders =
    {
        "changeme",
        "change-me",
        "change_me",
        "password",
        "default",
        "placeholder",
        "secret",
        "changeit",
        "todo",
    };

    /// <summary>
    /// Determines whether the supplied initial password is a refused default/placeholder (or is
    /// absent), meaning the bootstrap must not create an Owner with it.
    /// </summary>
    /// <param name="initialPassword">The operator-supplied initial password, if any.</param>
    /// <returns>
    /// <see langword="true"/> when the value is <see langword="null"/>, empty, whitespace, or a
    /// recognized placeholder; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsRefusedDefault(string? initialPassword)
    {
        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            return true;
        }

        string normalized = initialPassword.Trim();

        return s_knownPlaceholders.Any(
            placeholder => string.Equals(normalized, placeholder, System.StringComparison.OrdinalIgnoreCase));
    }
}
