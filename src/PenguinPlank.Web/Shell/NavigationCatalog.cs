namespace PenguinPlank.Web.Shell;

/// <summary>Decides which navigation entries a role may see (requirement A5 7.1).</summary>
/// <remarks>
/// Hiding an entry is a usability aid only; the API remains the authority for authorization.
/// </remarks>
public static class NavigationCatalog
{
    /// <summary>The role string that grants owner-only areas.</summary>
    public const string OwnerRole = "Owner";

    /// <summary>The route of the Products &amp; Stock entry (catalog views are delivered separately).</summary>
    public const string ProductsRoute = "catalog/products";

    private static readonly NavigationItem[] s_commonItems =
    [
        new("Dashboard", string.Empty),
        new("Products & Stock", ProductsRoute),
        new("Workshop", "workshop", IsPlaceholder: true),
        new("Markets", "markets", IsPlaceholder: true),
        new("Costing", "costing", IsPlaceholder: true),
        new("Photos & Listings", "photos-listings", IsPlaceholder: true),
        new("Wholesale", "wholesale", IsPlaceholder: true),
    ];

    private static readonly NavigationItem[] s_ownerItems =
    [
        new("Integrations", "integrations", IsPlaceholder: true),
        new("Settings", "settings", IsPlaceholder: true),
    ];

    /// <summary>Gets the navigation entries for a role.</summary>
    /// <param name="role">The actor's role string; <see langword="null"/> yields no entries.</param>
    /// <returns>The visible entries in display order.</returns>
    public static IReadOnlyList<NavigationItem> GetItems(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return [];
        }

        return IsOwner(role) ? [.. s_commonItems, .. s_ownerItems] : s_commonItems;
    }

    /// <summary>Determines whether a role string denotes the owner.</summary>
    /// <param name="role">The role string.</param>
    /// <returns><see langword="true"/> for the Owner role.</returns>
    public static bool IsOwner(string? role) =>
        string.Equals(role, OwnerRole, StringComparison.Ordinal);
}
