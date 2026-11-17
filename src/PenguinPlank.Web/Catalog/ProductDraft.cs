using PenguinPlank.Contracts.Catalog;

namespace PenguinPlank.Web.Catalog;

/// <summary>The editable form values for a product. The server remains the authority on validity.</summary>
public sealed class ProductDraft
{
    /// <summary>The longest name the form accepts before the server is asked.</summary>
    public const int MaximumNameLength = 200;

    /// <summary>Gets or sets the product name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the category.</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the public description.</summary>
    public string? PublicDescription { get; set; }

    /// <summary>Gets or sets the internal notes.</summary>
    public string? InternalNotes { get; set; }

    /// <summary>Creates a draft holding the values of a loaded product.</summary>
    /// <param name="product">The loaded product.</param>
    /// <returns>The draft.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="product"/> is <see langword="null"/>.</exception>
    public static ProductDraft From(ProductResponse product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new ProductDraft
        {
            Name = product.Name,
            Category = product.Category,
            PublicDescription = product.PublicDescription,
            InternalNotes = product.InternalNotes,
        };
    }

    /// <summary>Checks the values the form can verify without the server.</summary>
    /// <returns>The problems found, or an empty list.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add("Name is required.");
        }
        else if (Name.Trim().Length > MaximumNameLength)
        {
            problems.Add($"Name must be {MaximumNameLength} characters or fewer.");
        }

        return problems;
    }

    /// <summary>Builds the edit request, trimming text and treating blank optional text as absent.</summary>
    /// <returns>The request.</returns>
    public UpdateProductContract ToContract() => new()
    {
        Name = Name.Trim(),
        Category = DraftText.OrNull(Category),
        PublicDescription = DraftText.OrNull(PublicDescription),
        InternalNotes = DraftText.OrNull(InternalNotes),
    };
}
