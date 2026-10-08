namespace PenguinPlank.Application.Abstractions;

/// <summary>
/// The default <see cref="IIdentifierGenerator"/>: produces time-ordered GUID version 7 values.
/// </summary>
/// <remarks>
/// Version 7 GUIDs embed a timestamp prefix, so sequential inserts cluster in key order and avoid
/// the index fragmentation that random version 4 GUIDs cause on a clustered primary key (design
/// §"GUID key strategy avoiding avoidable fragmentation"). The type is stateless and holds no
/// scoped dependency, so the composition root registers it as a singleton. It is the single place
/// <see cref="System.Guid"/> creation happens for new catalog records; use cases depend on the
/// <see cref="IIdentifierGenerator"/> seam instead, which keeps them deterministic under test
/// (coding-standards §1, §2).
/// </remarks>
public sealed class GuidIdentifierGenerator : IIdentifierGenerator
{
    /// <inheritdoc />
    public Guid NewId() => Guid.CreateVersion7();
}
