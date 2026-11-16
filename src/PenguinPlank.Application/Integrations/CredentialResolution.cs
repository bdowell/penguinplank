using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The outcome of resolving a connection's credential reference. <see cref="Reference"/> is set
/// only when <see cref="Decision"/> is <see cref="CredentialUseDecision.Usable"/>, so a revoked
/// or expired credential's locator is never handed out (requirement R11 / 3.3).
/// </summary>
/// <param name="Decision">Whether the credential may be used, and if not, why.</param>
/// <param name="Reference">The secret-store locator when usable; otherwise null.</param>
public sealed record CredentialResolution(CredentialUseDecision Decision, CredentialReference? Reference);
