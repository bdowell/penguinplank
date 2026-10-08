using PenguinPlank.Application.Abstractions;

namespace PenguinPlank.Application.IdentityAdministration.Authorization;

/// <summary>
/// Projects a rich internal representation into a role-safe response shape, including only
/// the fields the actor's <see cref="Role"/> is explicitly allowed to see.
/// </summary>
/// <remarks>
/// <para>
/// This is the server-side field-level allowlist mechanism required by A2 §5.11. Use cases
/// never serialize an internal entity wholesale; they call a projector so that financial
/// fields (unit cost, margin, profit, wholesale cost) are populated only for an Owner and
/// are <b>structurally absent</b> from a Staff result — not present-but-null and not merely
/// hidden by a browser control. A projector returns a different DTO shape per role (an Owner
/// shape that carries the financial fields and a Staff shape that has no such members at
/// all), so a Staff response cannot carry financial data even in principle.
/// </para>
/// <para>
/// Implementations are pure: they take ordinary inputs and return a value, perform no I/O,
/// and read no clock, configuration, or ambient state. That keeps them directly testable
/// without starting the application (coding-standards §1, §7) and safe to register as a
/// singleton (requirement A2 §5.14) because they capture no scoped dependency such as a
/// <c>DbContext</c>.
/// </para>
/// </remarks>
/// <typeparam name="TSource">
/// The rich internal representation to project from. It may contain financial and other
/// Owner-only fields; it is never returned to a caller directly.
/// </typeparam>
/// <typeparam name="TResult">
/// The role-safe result. This is the base/common contract both role shapes satisfy; the
/// concrete object returned for an Owner is an Owner shape and for Staff a Staff shape.
/// </typeparam>
public interface IRoleProjector<in TSource, out TResult>
    where TResult : class
{
    /// <summary>
    /// Projects <paramref name="source"/> into the role-safe shape for <paramref name="role"/>.
    /// </summary>
    /// <param name="source">The rich internal representation to project.</param>
    /// <param name="role">The actor role that determines which fields are included.</param>
    /// <returns>
    /// A result containing only the fields <paramref name="role"/> is permitted to see.
    /// Owner-only fields are structurally absent from the Staff result.
    /// </returns>
    TResult Project(TSource source, Role role);
}
