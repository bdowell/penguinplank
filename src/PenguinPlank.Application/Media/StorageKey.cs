namespace PenguinPlank.Application.Media;

/// <summary>
/// A randomized, opaque key identifying a stored media file within an <see cref="IFileStore"/>
/// (requirement 6.9 / A4).
/// </summary>
/// <remarks>
/// <para>
/// A storage key is assigned by the file store on upload and is <b>never derived from the client
/// filename</b>, so a file's location is not guessable and the original name does not leak. The
/// value is a GUID-based token (see the infrastructure adapter). This value object only guarantees
/// the key is a non-empty, trimmed string; it carries no filesystem path and no visibility, so it
/// cannot by itself grant access — every download is authorized at the boundary (requirement 6.11).
/// </para>
/// <para>
/// It is an immutable value object, directly testable without the application (coding-standards §1).
/// </para>
/// </remarks>
public readonly record struct StorageKey
{
    /// <summary>
    /// Wraps an existing storage-key value (for example when reading a <c>MediaAsset.StorageKey</c>
    /// back from persistence to open or delete the file).
    /// </summary>
    /// <param name="value">The non-empty storage-key value.</param>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="value"/> is null or blank.</exception>
    public StorageKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new System.ArgumentException("A storage key must be a non-empty value.", nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>The opaque storage-key value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
