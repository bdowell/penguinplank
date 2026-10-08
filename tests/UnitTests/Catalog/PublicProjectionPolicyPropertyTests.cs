using System.Reflection;
using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;

namespace UnitTests.Catalog;

/// <summary>
/// Property 9 (requirements R01 Â§1.11, R10 Â§2.10, Â§2.11): <em>public projection safety.</em>
/// <see cref="PublicProjectionPolicy.ToPublicView"/> is a pure static function over an explicit
/// product and media collection â€” no I/O, clock, configuration, or ambient state â€” so these
/// properties exercise it with ordinary values and need no entities-from-a-database or application
/// startup (coding-standards Â§1, Â§7). The properties assert, across the whole input space, the two
/// public-safety guarantees plus the stronger no-internal-leak guarantee:
/// <list type="number">
///   <item><b>Draft is not visible.</b> When a product is not
///   <see cref="PublicationState.PublicApproved"/> (for example <see cref="PublicationState.Draft"/>),
///   the policy returns <see langword="null"/> â€” no partial public content ever escapes.</item>
///   <item><b>Public-approved projection.</b> When a product is
///   <see cref="PublicationState.PublicApproved"/>, the policy returns a non-null
///   <see cref="PublicProductView"/> whose Name/Category/PublicDescription equal the product's
///   public fields and whose Media contains exactly the <see cref="MediaVisibility.PublicApproved"/>
///   assets (never a <see cref="MediaVisibility.Private"/> one), ordered by
///   <see cref="MediaAsset.SortOrder"/>.</item>
///   <item><b>No internal leak (the key safety property).</b> The projection never carries an
///   internal value. This is asserted two ways: <em>structurally</em>, by reflecting over the public
///   members of <see cref="PublicProductView"/> and <see cref="PublicMediaView"/> to confirm no
///   internal/technical member (InternalNotes, PublicationState, RowVersion, Checksum, MimeType,
///   SizeBytes, Visibility) exists; and <em>by value</em>, by scanning every string the view exposes
///   and asserting the generated sentinel InternalNotes string and media Checksum/MimeType strings
///   never appear anywhere in the projection.</item>
/// </list>
/// </summary>
/// <remarks>
/// Generators produce products with random public fields, a distinctive sentinel InternalNotes
/// string (so a value leak would be detectable), and a random <see cref="PublicationState"/>; plus
/// media collections mixing <see cref="MediaVisibility.Private"/> and
/// <see cref="MediaVisibility.PublicApproved"/> assets with distinctive Checksum/MimeType values.
/// CsCheck (pinned in <c>UnitTests.csproj</c>) runs each property at <see cref="Iterations"/>
/// samples, well above the design's â‰¥100 floor.
/// </remarks>
public class PublicProjectionPolicyPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Public member names that are legitimately exposed by the public projection shapes. Any
    /// public property outside this allowlist on either public view type would be a structural leak.
    /// </summary>
    private static readonly HashSet<string> s_allowedViewMembers = new(StringComparer.Ordinal)
    {
        nameof(PublicProductView.Name),
        nameof(PublicProductView.Category),
        nameof(PublicProductView.PublicDescription),
        nameof(PublicProductView.Media),
        nameof(PublicMediaView.StorageReference),
        nameof(PublicMediaView.Caption),
        nameof(PublicMediaView.Role),
        nameof(PublicMediaView.SortOrder),
        // EqualityContract is a compiler-generated member of every record; it is not a data field.
        "EqualityContract",
    };

    /// <summary>
    /// Internal/technical member names that must never appear on a public view shape. Their mere
    /// presence as a member would be a structural leak even before any value is considered.
    /// </summary>
    private static readonly string[] s_forbiddenViewMembers =
    {
        nameof(Product.InternalNotes),
        nameof(Product.PublicationState),
        nameof(Product.ActiveFlag),
        "RowVersion",
        nameof(MediaAsset.Checksum),
        nameof(MediaAsset.MimeType),
        nameof(MediaAsset.SizeBytes),
        nameof(MediaAsset.Visibility),
        nameof(MediaAsset.StorageKey),
    };

    /// <summary>A short random non-empty alphanumeric-ish string used for public display fields.</summary>
    private static readonly Gen<string> s_genText =
        Gen.String[Gen.Char['a', 'z'], 1, 12];

    /// <summary>An optional random string: sometimes null, sometimes a short random string.</summary>
    private static readonly Gen<string?> s_genOptionalText =
        Gen.Frequency(
            (1, Gen.Const((string?)null)),
            (3, s_genText.Select(text => (string?)text)));

    /// <summary>
    /// Generates a product with random public fields and a distinctive sentinel InternalNotes value
    /// so a value leak into the projection would be detectable, plus a random publication state.
    /// </summary>
    private static readonly Gen<Product> s_genProduct =
        s_genText.SelectMany(name => s_genOptionalText.SelectMany(category =>
            s_genOptionalText.SelectMany(publicDescription => s_genText.SelectMany(secret =>
                Gen.Bool.Select(isApproved => new Product
                {
                    Name = name,
                    Category = category,
                    PublicDescription = publicDescription,
                    // A distinctive, improbable sentinel so any appearance in the projection is a leak.
                    InternalNotes = "INTERNAL_SECRET_" + secret + "_DO_NOT_LEAK",
                    PublicationState = isApproved
                        ? PublicationState.PublicApproved
                        : PublicationState.Draft,
                })))));

    /// <summary>
    /// Generates a single media asset with a random visibility and distinctive technical values
    /// (Checksum/MimeType) so a value leak would be detectable, plus display metadata.
    /// </summary>
    private static readonly Gen<MediaAsset> s_genMediaAsset =
        Gen.Guid.SelectMany(id => s_genText.SelectMany(checksum =>
            s_genOptionalText.SelectMany(caption => s_genOptionalText.SelectMany(role =>
                Gen.Int[0, 50].SelectMany(sortOrder =>
                    Gen.Bool.Select(isPublic => new MediaAsset
                    {
                        // A globally unique storage key per asset so distinct assets can never share
                        // a prefix; a prefix collision would make a substring-based leak scan report a
                        // false positive rather than a real leak.
                        StorageKey = "storage_" + id.ToString("N"),
                        // Distinctive, improbable sentinels so any appearance in the projection is a
                        // real leak and never an incidental substring collision.
                        Checksum = "CHECKSUM_SECRET_" + id.ToString("N") + "_" + checksum + "_DO_NOT_LEAK",
                        MimeType = "MIME_SECRET_" + id.ToString("N") + "_" + checksum + "_DO_NOT_LEAK",
                        SizeBytes = 1234,
                        Caption = caption,
                        Role = role,
                        SortOrder = sortOrder,
                        Visibility = isPublic
                            ? MediaVisibility.PublicApproved
                            : MediaVisibility.Private,
                    }))))));

    /// <summary>A collection of 0..8 media assets with mixed visibility.</summary>
    private static readonly Gen<MediaAsset[]> s_genMediaCollection =
        s_genMediaAsset.Array[0, 8];

    [Fact]
    public void ToPublicView_ProductNotPublicApproved_ReturnsNull()
    {
        // Constrain to non-approved products (Draft) so the only outcome under test is null.
        Gen<Product> genDraft = s_genProduct
            .Where(product => product.PublicationState != PublicationState.PublicApproved);

        genDraft.SelectMany(product => s_genMediaCollection
                .Select(media => (product, media)))
            .Sample(
                sample =>
                {
                    PublicProductView? view =
                        PublicProjectionPolicy.ToPublicView(sample.product, sample.media);

                    Assert.Null(view);
                },
                iter: Iterations);
    }

    [Fact]
    public void ToPublicView_PublicApproved_ProjectsPublicFieldsAndApprovedMediaInSortOrder()
    {
        // Constrain to approved products so a non-null view is always produced.
        Gen<Product> genApproved = s_genProduct
            .Where(product => product.PublicationState == PublicationState.PublicApproved);

        genApproved.SelectMany(product => s_genMediaCollection
                .Select(media => (product, media)))
            .Sample(
                sample =>
                {
                    PublicProductView? view =
                        PublicProjectionPolicy.ToPublicView(sample.product, sample.media);

                    Assert.NotNull(view);
                    Assert.Equal(sample.product.Name, view!.Name);
                    Assert.Equal(sample.product.Category, view.Category);
                    Assert.Equal(sample.product.PublicDescription, view.PublicDescription);

                    // Media contains exactly the public-approved assets, none of the private ones,
                    // ordered by SortOrder.
                    MediaAsset[] expectedApproved = sample.media
                        .Where(asset => asset.Visibility == MediaVisibility.PublicApproved)
                        .OrderBy(asset => asset.SortOrder)
                        .ToArray();

                    Assert.Equal(expectedApproved.Length, view.Media.Count);

                    for (int i = 0; i < expectedApproved.Length; i++)
                    {
                        Assert.Equal(expectedApproved[i].StorageKey, view.Media[i].StorageReference);
                        Assert.Equal(expectedApproved[i].Caption, view.Media[i].Caption);
                        Assert.Equal(expectedApproved[i].Role, view.Media[i].Role);
                        Assert.Equal(expectedApproved[i].SortOrder, view.Media[i].SortOrder);
                    }

                    // No private asset's storage key is present in the projection.
                    IEnumerable<string> privateKeys = sample.media
                        .Where(asset => asset.Visibility == MediaVisibility.Private)
                        .Select(asset => asset.StorageKey);

                    foreach (string privateKey in privateKeys)
                    {
                        Assert.DoesNotContain(
                            view.Media,
                            projected => projected.StorageReference == privateKey
                                && sample.media.All(asset =>
                                    asset.StorageKey != privateKey
                                    || asset.Visibility == MediaVisibility.Private));
                    }

                    // SortOrder is non-decreasing in the projected media.
                    for (int i = 1; i < view.Media.Count; i++)
                    {
                        Assert.True(view.Media[i - 1].SortOrder <= view.Media[i].SortOrder);
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void PublicViewShapes_StructurallyDeclareOnlyAllowlistedPublicMembers()
    {
        // Structural guarantee: no internal/technical member exists on either public view type, so
        // the policy is incapable of copying one across even in principle. This is independent of
        // the generated samples, but asserted here alongside the value checks for completeness.
        AssertTypeHasNoForbiddenMembers(typeof(PublicProductView));
        AssertTypeHasNoForbiddenMembers(typeof(PublicMediaView));
    }

    [Fact]
    public void ToPublicView_PublicApproved_NeverLeaksInternalOrTechnicalValuesByValue()
    {
        Gen<Product> genApproved = s_genProduct
            .Where(product => product.PublicationState == PublicationState.PublicApproved);

        genApproved.SelectMany(product => s_genMediaCollection
                .Select(media => (product, media)))
            .Sample(
                sample =>
                {
                    PublicProductView? view =
                        PublicProjectionPolicy.ToPublicView(sample.product, sample.media);

                    Assert.NotNull(view);

                    // Gather every string the projection exposes, product-level and media-level.
                    List<string> projectedStrings = CollectProjectedStrings(view!);

                    // The product's sentinel internal notes must appear nowhere in the projection.
                    string internalSecret = sample.product.InternalNotes!;
                    foreach (string projected in projectedStrings)
                    {
                        Assert.DoesNotContain(internalSecret, projected, StringComparison.Ordinal);
                    }

                    // No media asset's technical values (Checksum, MimeType) appear anywhere, and no
                    // private asset's storage key appears either.
                    foreach (MediaAsset asset in sample.media)
                    {
                        foreach (string projected in projectedStrings)
                        {
                            Assert.DoesNotContain(asset.Checksum, projected, StringComparison.Ordinal);
                            Assert.DoesNotContain(asset.MimeType, projected, StringComparison.Ordinal);
                        }

                        if (asset.Visibility == MediaVisibility.Private)
                        {
                            foreach (string projected in projectedStrings)
                            {
                                Assert.DoesNotContain(
                                    asset.StorageKey,
                                    projected,
                                    StringComparison.Ordinal);
                            }
                        }
                    }
                },
                iter: Iterations);
    }

    /// <summary>
    /// Asserts a public view type exposes only allowlisted public instance properties and declares
    /// none of the forbidden internal/technical members.
    /// </summary>
    private static void AssertTypeHasNoForbiddenMembers(Type viewType)
    {
        PropertyInfo[] publicProperties = viewType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in publicProperties)
        {
            Assert.True(
                s_allowedViewMembers.Contains(property.Name),
                $"{viewType.Name} exposes an unexpected public member '{property.Name}', " +
                "which may be an internal leak.");
        }

        HashSet<string> declaredNames = publicProperties
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string forbidden in s_forbiddenViewMembers)
        {
            Assert.DoesNotContain(forbidden, declaredNames);
        }
    }

    /// <summary>
    /// Collects every string value the projection exposes â€” the product-level public fields and
    /// each media view's string fields â€” so a value-leak scan can inspect all of them.
    /// </summary>
    private static List<string> CollectProjectedStrings(PublicProductView view)
    {
        List<string> values = [view.Name];

        if (view.Category is not null)
        {
            values.Add(view.Category);
        }

        if (view.PublicDescription is not null)
        {
            values.Add(view.PublicDescription);
        }

        foreach (PublicMediaView media in view.Media)
        {
            values.Add(media.StorageReference);

            if (media.Caption is not null)
            {
                values.Add(media.Caption);
            }

            if (media.Role is not null)
            {
                values.Add(media.Role);
            }
        }

        return values;
    }
}
