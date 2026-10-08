namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Reserves the Integrations module boundary within the Application layer.
/// </summary>
/// <remarks>
/// Integrations is an <b>implemented</b> Phase A module (connectors disabled). Its
/// use-case orchestration and narrow boundary interfaces (connection/credential-reference
/// stores, mapping store, durable inbox/outbox, sync checkpoints) are added here by later
/// tasks. This marker exists only so the module folder and
/// <c>PenguinPlank.Application.Integrations</c> namespace are real and version-controlled;
/// it carries no business logic (coding-standards §1, §3).
/// </remarks>
internal sealed class ModulePlaceholder
{
}
