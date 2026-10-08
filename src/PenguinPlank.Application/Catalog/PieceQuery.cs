using PenguinPlank.Application.Common;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The filter and paging intent for listing <c>ProductPiece</c> read models.
/// </summary>
/// <remarks>
/// An immutable query value carried across the read boundary. Pieces belong to serialized
/// variants, so <see cref="VariantId"/> is the primary filter. Null filter members mean "no
/// restriction"; the reader normalizes <see cref="Page"/> and returns a
/// <see cref="Common.Page{T}"/> with a total count (requirement A4 §6.1). No persistence type
/// leaks across the boundary (coding-standards §2, §3).
/// </remarks>
public sealed record PieceQuery
{
    /// <summary>Restricts results to pieces of a single serialized variant, when supplied.</summary>
    public Guid? VariantId { get; init; }

    /// <summary>A free-text term matched against the piece code and story, when supplied.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Restricts results to a single publication state, when supplied.</summary>
    public PublicationState? PublicationState { get; init; }

    /// <summary>
    /// Whether to include archived (inactive) pieces. Defaults to <see langword="false"/>;
    /// archived records remain readable when explicitly requested (requirement 1.8).
    /// </summary>
    public bool IncludeArchived { get; init; }

    /// <summary>The paging and stable-sort intent. Defaults to the first page at the default size.</summary>
    public PageRequest Page { get; init; } = PageRequest.Default;
}
