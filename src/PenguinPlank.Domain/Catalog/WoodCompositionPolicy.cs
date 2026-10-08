using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// Pure validation of the wood-composition proportions declared for a
/// <see cref="ProductVariant"/> or <see cref="ProductPiece"/>.
/// </summary>
/// <remarks>
/// <para>
/// A composition associates one or more <see cref="WoodSpecies"/> with a variant or piece,
/// each association carrying an <em>optional</em> proportion (<see cref="VariantWood.Proportion"/> /
/// <see cref="PieceWood.Proportion"/>, a nullable <see cref="decimal"/>). This policy enforces the
/// only rule the specification places on those proportions: when a proportion is supplied it must
/// fall within 0 to 100 percent inclusive (requirements R01 / 1.5 and R10 / 2.2).
/// </para>
/// <para>
/// <b>Range only — no sum rule.</b> R01 / 1.5 states that each association has an optional
/// proportion and that the composition is rejected only "IF any supplied proportion is less than 0
/// or greater than 100 percent". R10 / 2.2 only requires that multiple species can be represented
/// rather than a single wood text field. Neither requirement, nor the design's Property 3
/// ("accepted if and only if every supplied proportion lies within 0 to 100 percent inclusive"),
/// requires a fully specified set of proportions to sum to 100. Proportions are therefore treated
/// as individually bounded, informational values: a <see langword="null"/> proportion is an
/// unquantified presence and is always acceptable, and a set that does not sum to 100 is still
/// acceptable so long as every supplied value is in range.
/// </para>
/// <para>
/// The policy is a pure static function over an explicit collection of nullable proportions, so it
/// is directly testable without constructing entities, a database, or application startup
/// (coding-standards §1). The caller is responsible for applying the returned
/// <see cref="Result"/>: on failure it must leave the variant or piece composition unchanged
/// (requirement R01 / 1.5).
/// </para>
/// </remarks>
public static class WoodCompositionPolicy
{
    /// <summary>The inclusive lower bound for a supplied wood proportion, as a percentage.</summary>
    public const decimal MinimumProportion = 0m;

    /// <summary>The inclusive upper bound for a supplied wood proportion, as a percentage.</summary>
    public const decimal MaximumProportion = 100m;

    /// <summary>
    /// Validates the proportions declared across a wood composition.
    /// </summary>
    /// <param name="proportions">
    /// The proportion of each <see cref="WoodSpecies"/> association in the composition, in any
    /// order. A <see langword="null"/> entry represents an association with no quantified
    /// proportion and is always accepted. An empty or <see langword="null"/> collection represents
    /// a composition with no proportion constraints to check and is accepted.
    /// </param>
    /// <returns>
    /// <see cref="Result.Success()"/> when every supplied (non-null) proportion lies within
    /// <see cref="MinimumProportion"/> to <see cref="MaximumProportion"/> inclusive; otherwise
    /// <see cref="Result.Failure(ErrorCode, string)"/> carrying
    /// <see cref="ErrorCode.InvalidProportion"/>.
    /// </returns>
    public static Result Validate(IEnumerable<decimal?>? proportions)
    {
        if (proportions is null)
        {
            return Result.Success();
        }

        foreach (decimal? proportion in proportions)
        {
            if (!IsSuppliedProportionInRange(proportion))
            {
                return Result.Failure(
                    ErrorCode.InvalidProportion,
                    "Each supplied wood proportion must be between 0 and 100 percent inclusive.");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Determines whether a single proportion value is acceptable: either absent
    /// (<see langword="null"/>) or within the inclusive 0 to 100 percent range.
    /// </summary>
    /// <param name="proportion">The optional proportion to check.</param>
    /// <returns>
    /// <see langword="true"/> when the proportion is <see langword="null"/> or in range;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsSuppliedProportionInRange(decimal? proportion) =>
        proportion is not decimal value
        || (value >= MinimumProportion && value <= MaximumProportion);
}
