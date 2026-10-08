namespace PenguinPlank.Application.Abstractions;

/// <summary>
/// Generates the stable identifiers a use case assigns to new records.
/// </summary>
/// <remarks>
/// <para>
/// Coding-standards §2 asks that a generated identifier be passed as an input or produced by a
/// small injected generator when its value affects observable behavior. A use case that creates a
/// variant, piece, channel listing, or care version returns the new record's identifier, so the
/// value is observable. Depending on this narrow seam rather than calling
/// <see cref="System.Guid.NewGuid"/> inside a business method keeps the use case deterministic
/// under test: a substitute can hand back known identifiers so an assertion can name the exact id
/// a successful create returns (coding-standards §1, §2).
/// </para>
/// <para>
/// The default production implementation is <see cref="GuidIdentifierGenerator"/>
/// (<c>Guid.CreateVersion7</c>, time-ordered to reduce index fragmentation on GUID keys — design
/// §"GUID key strategy"). It is registered as a singleton in the composition root; it captures no
/// scoped state.
/// </para>
/// </remarks>
public interface IIdentifierGenerator
{
    /// <summary>Produces a new unique identifier for a record about to be created.</summary>
    /// <returns>A new, non-empty <see cref="System.Guid"/>.</returns>
    Guid NewId();
}
