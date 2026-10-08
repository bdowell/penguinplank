namespace PenguinPlank.Application.Abstractions;

/// <summary>
/// The authorization role carried by an authenticated staff account.
/// </summary>
/// <remarks>
/// Phase A defines exactly two roles (requirements A2 §5.9, §5.10). <see cref="Owner"/>
/// has access to all modules, financial data, user administration, configuration,
/// imports, and stock adjustments. <see cref="Staff"/> has access to catalog and
/// operational work but never to unit cost, margin, profit, financial reports, or
/// user administration. The role travels inside <see cref="ActorContext"/>; it is
/// resolved at the API boundary and passed explicitly into use cases so that domain
/// and application logic never inspect <c>HttpContext</c> (coding-standards §2, §3).
/// </remarks>
public enum Role
{
    /// <summary>Full access to every module, including financials and administration.</summary>
    Owner = 0,

    /// <summary>Operational access only; never financial projections or user administration.</summary>
    Staff = 1,
}
