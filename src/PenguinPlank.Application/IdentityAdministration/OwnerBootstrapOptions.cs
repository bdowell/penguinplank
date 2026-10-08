namespace PenguinPlank.Application.IdentityAdministration;

/// <summary>
/// Operator-supplied configuration for the one-time Owner bootstrap: the first Owner's
/// email and the initial password, both provided at deployment time (requirement A2 §5.8).
/// </summary>
/// <remarks>
/// <para>
/// These values are bound from configuration — a deployment secret, a user-secrets entry,
/// or an environment variable surfaced through the configuration system — and validated as
/// typed options in the composition root (coding-standards §2). Business logic never reads
/// an environment variable or a configuration global directly; it receives this object.
/// </para>
/// <para>
/// There is <b>no default production password</b>. If <see cref="InitialPassword"/> is
/// absent, blank, or an obvious placeholder, the bootstrap refuses to create the Owner
/// rather than falling back to a baked-in secret. The refusal rule is implemented as a pure
/// function (<see cref="OwnerBootstrapPasswordPolicy"/>) so it is testable without starting
/// the application.
/// </para>
/// </remarks>
public sealed class OwnerBootstrapOptions
{
    /// <summary>
    /// The configuration section these options bind from
    /// (for example <c>OwnerBootstrap:Email</c> and <c>OwnerBootstrap:InitialPassword</c>).
    /// </summary>
    public const string SectionName = "OwnerBootstrap";

    /// <summary>
    /// The email address (and username) of the first Owner account. Required; the bootstrap
    /// refuses to run when it is missing or blank.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The initial password for the first Owner account, supplied by the operator at
    /// deployment time. Required; a missing, blank, or placeholder value causes the bootstrap
    /// to refuse rather than use a default. The value must also satisfy the configured Identity
    /// password policy.
    /// </summary>
    public string? InitialPassword { get; set; }
}
