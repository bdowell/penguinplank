namespace PenguinPlank.Application.Media;

/// <summary>
/// Generates the randomized, unique <see cref="StorageKey"/> assigned to a media file on upload
/// (requirement 6.9 / A4).
/// </summary>
/// <remarks>
/// <para>
/// The key must be unguessable and <b>never derived from the client filename</b>. Injecting the
/// generator (rather than calling <c>Guid.NewGuid()</c> inside a business method) lets a test supply
/// a controllable substitute when the key value affects observable behavior, keeping the file store
/// deterministically testable (coding-standards §2). The default implementation is GUID-based.
/// </para>
/// </remarks>
public interface IStorageKeyGenerator
{
    /// <summary>Produces a fresh, randomized, unique storage key.</summary>
    /// <returns>A new <see cref="StorageKey"/>.</returns>
    StorageKey NewKey();
}
