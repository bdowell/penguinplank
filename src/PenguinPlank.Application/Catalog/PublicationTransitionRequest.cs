namespace PenguinPlank.Application.Catalog;

/// <summary>
/// A request to transition the publication state of a catalog record (publish or withdraw),
/// including the concurrency token the edit must match.
/// </summary>
/// <remarks>
/// <para>
/// Publication is an <b>explicit</b> action: content is only exposed after a deliberate publish
/// and is never published automatically (requirements 1.12, 2.6, 2.7). The same operation
/// withdraws content back to draft. The target is named through a <see cref="CatalogRef"/>, and
/// because publication state lives on a mutable aggregate the request carries the
/// <see cref="ExpectedVersion"/> opaque token so a stale transition is rejected rather than
/// silently overwriting newer state (requirements 6.5, 6.6). This is an immutable request value
/// with no I/O (coding-standards §1).
/// </para>
/// </remarks>
public sealed record PublicationTransitionRequest
{
    /// <summary>Creates a publication-transition request for a target catalog record.</summary>
    /// <param name="target">The product, variant, or piece whose publication state is changing.</param>
    /// <param name="expectedVersion">The opaque concurrency token read with the record (the encoded ETag).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expectedVersion"/> is null or whitespace.</exception>
    public PublicationTransitionRequest(CatalogRef target, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (string.IsNullOrWhiteSpace(expectedVersion))
        {
            throw new ArgumentException(
                "A publication transition requires the record's concurrency token.",
                nameof(expectedVersion));
        }

        Target = target;
        ExpectedVersion = expectedVersion;
    }

    /// <summary>The product, variant, or piece whose publication state is changing.</summary>
    public CatalogRef Target { get; }

    /// <summary>The opaque concurrency token read with the record (the encoded ETag/If-Match value).</summary>
    public string ExpectedVersion { get; }
}
