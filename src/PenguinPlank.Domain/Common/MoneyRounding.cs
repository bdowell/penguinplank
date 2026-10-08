namespace PenguinPlank.Domain.Common;

/// <summary>
/// The single, authoritative policy for rounding USD monetary amounts at transaction
/// boundaries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> Money is represented as <see cref="decimal"/> throughout
/// the application and stored as <c>decimal(19,4)</c> (coding-standards §5, §6; design
/// "Global column and type conventions"). Rounding, however, is a <em>business decision</em>:
/// it must happen once, consistently, at the point a transaction's monetary outcome is
/// finalized (a sale total, a recognized cost, an event contribution). Scattering
/// <see cref="decimal.Round(decimal, int)"/> calls through use cases invites inconsistent
/// policies and off-by-a-cent drift. Centralizing the rule here keeps it pure, testable,
/// and impossible to apply two different ways.
/// </para>
/// <para>
/// <b>Decimal places.</b> Amounts are rounded to <see cref="DecimalPlaces"/> (4) decimal
/// places, matching the <c>decimal(19,4)</c> storage precision. Callers that need to
/// present a cent-rounded figure (2 places) do so at the presentation boundary; the stored
/// and calculated authoritative value keeps the 4-place scale so intermediate sums do not
/// lose fractional cents prematurely.
/// </para>
/// <para>
/// <b>Rounding mode.</b> The policy uses <see cref="System.MidpointRounding.ToEven"/>
/// ("banker's rounding"). This is the deliberate choice for financial aggregation: over a
/// large number of midpoint cases it does not systematically bias totals upward the way
/// away-from-zero rounding does, which matters for recognized cost and revenue summaries.
/// The mode is fixed here so every monetary rounding in the system shares it.
/// </para>
/// <para>
/// This is a pure function with no dependencies — no clock, database, or configuration — so
/// it is exercised directly with ordinary values (coding-standards §1).
/// </para>
/// </remarks>
public static class MoneyRounding
{
    /// <summary>
    /// The number of decimal places USD amounts are rounded to, matching the
    /// <c>decimal(19,4)</c> money storage precision.
    /// </summary>
    public const int DecimalPlaces = 4;

    /// <summary>
    /// The fixed midpoint-rounding mode applied to every monetary rounding:
    /// <see cref="System.MidpointRounding.ToEven"/> (banker's rounding).
    /// </summary>
    public const System.MidpointRounding Mode = System.MidpointRounding.ToEven;

    /// <summary>
    /// Rounds a USD amount to the authoritative money scale using the fixed policy
    /// (<see cref="DecimalPlaces"/> places, <see cref="Mode"/>).
    /// </summary>
    /// <param name="amount">The unrounded monetary amount, in USD.</param>
    /// <returns>
    /// The amount rounded to <see cref="DecimalPlaces"/> decimal places using
    /// banker's rounding.
    /// </returns>
    public static decimal Round(decimal amount) =>
        decimal.Round(amount, DecimalPlaces, Mode);
}
