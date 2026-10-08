using PenguinPlank.Application.Media;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// The default <see cref="IStorageKeyGenerator"/>: produces a GUID-based, randomized storage key
/// (requirement 6.9 / A4).
/// </summary>
/// <remarks>
/// <para>
/// Each key is a fresh <see cref="System.Guid"/> rendered in the compact "N" format (32 lowercase
/// hex digits, no hyphens). A version-4 GUID is random and unguessable, and the key is derived
/// solely from the GUID — never from the client filename — so a stored file's location cannot be
/// predicted from its upload name (requirement 6.9).
/// </para>
/// <para>
/// This is a stateless, thread-safe service with no captured dependency, so the composition root
/// registers it as a singleton (coding-standards §2).
/// </para>
/// </remarks>
public sealed class GuidStorageKeyGenerator : IStorageKeyGenerator
{
    /// <inheritdoc />
    public StorageKey NewKey() => new(System.Guid.NewGuid().ToString("N"));
}
