namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The public-safe projection of a single approved media association on a
/// <see cref="PublicProductView"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>distinct public shape</b>, not a reshaped <c>MediaAsset</c>. It carries only the
/// members an anonymous or customer-facing catalog consumer may see — the opaque storage
/// reference needed to request an (independently authorized) download, plus display metadata.
/// It deliberately omits the asset's technical and internal metadata (checksum, byte size, MIME
/// type, raw visibility state), so a public media entry is structurally incapable of leaking
/// those values (requirements R01 1.11, R10 2.10, 2.11; design Property 9).
/// </para>
/// <para>
/// Exposing <c>StorageReference</c> is not the same as granting access: it is an opaque,
/// randomized key, and every media download remains authorized at the API boundary — public
/// approval is never, by itself, anonymous access (requirement 6.11).
/// </para>
/// </remarks>
public sealed record PublicMediaView
{
    /// <summary>Creates a public-safe media view.</summary>
    /// <param name="storageReference">The opaque storage key used to request a download.</param>
    /// <param name="caption">The optional human-readable caption.</param>
    /// <param name="role">The optional display role (for example, "Primary" or "Gallery").</param>
    /// <param name="sortOrder">The sort order within the asset's role or gallery.</param>
    public PublicMediaView(
        string storageReference,
        string? caption,
        string? role,
        int sortOrder)
    {
        StorageReference = storageReference;
        Caption = caption;
        Role = role;
        SortOrder = sortOrder;
    }

    /// <summary>The opaque storage key used to request an independently authorized download.</summary>
    public string StorageReference { get; }

    /// <summary>The optional human-readable caption for the asset.</summary>
    public string? Caption { get; }

    /// <summary>The optional display role of the asset (for example, "Primary" or "Gallery").</summary>
    public string? Role { get; }

    /// <summary>The sort order of the asset within its role or gallery.</summary>
    public int SortOrder { get; }
}
