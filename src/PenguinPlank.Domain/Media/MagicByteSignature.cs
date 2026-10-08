namespace PenguinPlank.Domain.Media;

/// <summary>
/// A position-anchored magic-byte signature: an expected byte sequence that must appear at a given
/// offset within a file's leading bytes (requirement 6.9 / A4).
/// </summary>
/// <remarks>
/// <para>
/// Most container formats place a fixed marker at offset 0 (PNG, GIF, JPEG). A few place a second
/// marker a short distance in — for example WebP's <c>WEBP</c> tag at offset 8 and the MP4/QuickTime
/// <c>ftyp</c> box tag at offset 4. A signature therefore carries an explicit <see cref="Offset"/>
/// and the <see cref="Pattern"/> expected there. A <see cref="Pattern"/> byte of value is matched
/// exactly; this type intentionally supports only exact bytes to keep the check simple and
/// auditable (coding-standards §5).
/// </para>
/// <para>
/// The type is an immutable value object with no I/O. It is used both to <em>accept</em> allowlisted
/// types (<see cref="MediaContentSignature"/>) and to <em>reject</em> dangerous content
/// (<see cref="MediaUploadValidationPolicy"/>'s executable/HTML signatures).
/// </para>
/// </remarks>
public sealed class MagicByteSignature
{
    private readonly byte[] _pattern;

    /// <summary>
    /// Creates a signature matched at the given offset.
    /// </summary>
    /// <param name="offset">The zero-based byte offset at which the pattern must appear. Non-negative.</param>
    /// <param name="pattern">The exact bytes expected at <paramref name="offset"/>; must be non-empty.</param>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="pattern"/> is empty.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is negative.</exception>
    public MagicByteSignature(int offset, params byte[] pattern)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegative(offset);
        System.ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.Length == 0)
        {
            throw new System.ArgumentException("A magic-byte signature requires a non-empty pattern.", nameof(pattern));
        }

        Offset = offset;
        _pattern = pattern.ToArray();
    }

    /// <summary>Creates a signature matched at offset 0.</summary>
    /// <param name="pattern">The exact bytes expected at the start of the content.</param>
    public MagicByteSignature(params byte[] pattern)
        : this(0, pattern)
    {
    }

    /// <summary>The zero-based offset at which <see cref="Pattern"/> must appear.</summary>
    public int Offset { get; }

    /// <summary>The exact bytes expected at <see cref="Offset"/>.</summary>
    public IReadOnlyList<byte> Pattern => _pattern;

    /// <summary>The minimum number of leading bytes required to evaluate this signature.</summary>
    public int RequiredLength => Offset + _pattern.Length;

    /// <summary>
    /// Determines whether <paramref name="leadingBytes"/> contains this signature's pattern at the
    /// expected offset.
    /// </summary>
    /// <param name="leadingBytes">The file's leading bytes.</param>
    /// <returns>
    /// <see langword="true"/> when the leading bytes are long enough and the bytes at the offset
    /// equal the pattern; otherwise <see langword="false"/>.
    /// </returns>
    public bool Matches(System.ReadOnlySpan<byte> leadingBytes)
    {
        if (leadingBytes.Length < RequiredLength)
        {
            return false;
        }

        return leadingBytes.Slice(Offset, _pattern.Length).SequenceEqual(_pattern);
    }
}
