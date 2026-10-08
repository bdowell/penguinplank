namespace PenguinPlank.Infrastructure.Persistence;

/// <summary>
/// The authoritative SQL precision/scale pairs for the application's decimal column
/// families, stated once so entity configurations never scatter magic
/// <c>precision, scale</c> literals (coding-standards §5; design "Global column and type
/// conventions").
/// </summary>
/// <remarks>
/// EF Core cannot infer a business-meaningful scale for a bare <see cref="decimal"/>, and a
/// single model-wide default cannot serve money, rates, and dimensions at once because
/// they use different scales. This type names each family; entity configurations added by
/// tasks 3.2–3.4 apply the matching pair with
/// <c>HasPrecision(DecimalPrecision.MoneyPrecision, DecimalPrecision.MoneyScale)</c>. The
/// context applies <see cref="MoneyPrecision"/>/<see cref="MoneyScale"/> as the model-wide
/// default so an unconfigured monetary column cannot silently fall back to EF's lossy
/// default scale.
/// </remarks>
public static class DecimalPrecision
{
    /// <summary>Total digits for money columns: <c>decimal(19,4)</c>.</summary>
    public const int MoneyPrecision = 19;

    /// <summary>Scale (decimal places) for money columns: <c>decimal(19,4)</c>.</summary>
    public const int MoneyScale = 4;

    /// <summary>Total digits for rate columns: <c>decimal(19,6)</c>.</summary>
    public const int RatePrecision = 19;

    /// <summary>Scale (decimal places) for rate columns: <c>decimal(19,6)</c>.</summary>
    public const int RateScale = 6;

    /// <summary>Total digits for dimension columns: <c>decimal(12,4)</c>.</summary>
    public const int DimensionPrecision = 12;

    /// <summary>Scale (decimal places) for dimension columns: <c>decimal(12,4)</c>.</summary>
    public const int DimensionScale = 4;
}
