namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The inputs required to replace the wood composition of a catalog record (a variant or a
/// piece).
/// </summary>
/// <remarks>
/// A variant or piece may be composed of more than one wood species (requirements 1.5, 2.2).
/// This request names the target through a <see cref="CatalogRef"/> and supplies the full desired
/// set of <see cref="WoodComponent"/> entries; the use case validates each proportion against the
/// 0–100 range and replaces the composition atomically. An empty <see cref="Components"/> set
/// clears the composition. This is an immutable request value with no I/O (coding-standards §1).
/// </remarks>
public sealed record SetWoodCompositionRequest
{
    /// <summary>Creates a wood-composition request for a target catalog record.</summary>
    /// <param name="target">The variant or piece whose composition is being set.</param>
    /// <param name="components">The full desired set of composition components; empty clears the composition.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> or <paramref name="components"/> is <see langword="null"/>.</exception>
    public SetWoodCompositionRequest(CatalogRef target, IReadOnlyList<WoodComponent> components)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(components);

        Target = target;
        Components = components;
    }

    /// <summary>The variant or piece whose composition is being set.</summary>
    public CatalogRef Target { get; }

    /// <summary>The full desired set of composition components. Empty clears the composition.</summary>
    public IReadOnlyList<WoodComponent> Components { get; }
}
