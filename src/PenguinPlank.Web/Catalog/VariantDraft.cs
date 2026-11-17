using PenguinPlank.Contracts.Catalog;

namespace PenguinPlank.Web.Catalog;

/// <summary>
/// The editable form values for a variant. SKU and tracking mode are not editable here; the server
/// is the authority on validity.
/// </summary>
public sealed class VariantDraft
{
    /// <summary>Gets or sets the unit of measure.</summary>
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>Gets or sets the barcode.</summary>
    public string? Barcode { get; set; }

    /// <summary>Gets or sets the finish.</summary>
    public string? Finish { get; set; }

    /// <summary>Gets or sets the retail price in USD.</summary>
    public decimal? RetailPrice { get; set; }

    /// <summary>Gets or sets the wholesale price in USD.</summary>
    public decimal? WholesalePrice { get; set; }

    /// <summary>Gets or sets the wholesale case pack.</summary>
    public int? CasePack { get; set; }

    /// <summary>Gets or sets the length.</summary>
    public decimal? Length { get; set; }

    /// <summary>Gets or sets the width.</summary>
    public decimal? Width { get; set; }

    /// <summary>Gets or sets the thickness.</summary>
    public decimal? Thickness { get; set; }

    /// <summary>Gets or sets the diameter.</summary>
    public decimal? Diameter { get; set; }

    /// <summary>Gets or sets the unit the dimensions are measured in.</summary>
    public string? DimensionUnit { get; set; }

    /// <summary>Gets or sets the care profile reference, carried through unchanged.</summary>
    public Guid? CareProfileId { get; set; }

    private bool HasDimensionValue => Length is not null || Width is not null || Thickness is not null || Diameter is not null;

    /// <summary>Creates a draft holding the values of a loaded variant.</summary>
    /// <param name="variant">The loaded variant.</param>
    /// <returns>The draft.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variant"/> is <see langword="null"/>.</exception>
    public static VariantDraft From(VariantResponse variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return new VariantDraft
        {
            UnitOfMeasure = variant.UnitOfMeasure,
            Barcode = variant.Barcode,
            Finish = variant.Finish,
            RetailPrice = variant.RetailPrice,
            WholesalePrice = variant.WholesalePrice,
            CasePack = variant.CasePack,
            Length = variant.Dimensions?.Length,
            Width = variant.Dimensions?.Width,
            Thickness = variant.Dimensions?.Thickness,
            Diameter = variant.Dimensions?.Diameter,
            DimensionUnit = variant.Dimensions?.Unit,
            CareProfileId = variant.CareProfileId,
        };
    }

    /// <summary>Checks the values the form can verify without the server.</summary>
    /// <returns>The problems found, or an empty list.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(UnitOfMeasure))
        {
            problems.Add("Unit of measure is required.");
        }

        if (RetailPrice is < 0)
        {
            problems.Add("Retail price cannot be negative.");
        }

        if (WholesalePrice is < 0)
        {
            problems.Add("Wholesale price cannot be negative.");
        }

        if (CasePack is < 1)
        {
            problems.Add("Case pack must be at least 1.");
        }

        if (HasDimensionValue && string.IsNullOrWhiteSpace(DimensionUnit))
        {
            problems.Add("Choose a unit for the dimensions.");
        }

        if (new[] { Length, Width, Thickness, Diameter }.Any(value => value is <= 0))
        {
            problems.Add("Dimensions must be greater than zero.");
        }

        return problems;
    }

    /// <summary>Builds the edit request, trimming text and treating blank optional text as absent.</summary>
    /// <returns>The request.</returns>
    public UpdateVariantContract ToContract() => new()
    {
        UnitOfMeasure = UnitOfMeasure.Trim(),
        Barcode = DraftText.OrNull(Barcode),
        Finish = DraftText.OrNull(Finish),
        RetailPrice = RetailPrice,
        WholesalePrice = WholesalePrice,
        CasePack = CasePack,
        CareProfileId = CareProfileId,
        Dimensions = HasDimensionValue
            ? new DimensionsContract
            {
                Length = Length,
                Width = Width,
                Thickness = Thickness,
                Diameter = Diameter,
                Unit = DraftText.OrNull(DimensionUnit),
            }
            : null,
    };
}
