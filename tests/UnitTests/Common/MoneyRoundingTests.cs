using PenguinPlank.Domain.Common;

namespace UnitTests.Common;

/// <summary>
/// Behavior of the <see cref="MoneyRounding"/> USD policy. Pure, no I/O
/// (coding-standards §1, §7).
/// </summary>
/// <remarks>
/// These tests pin the policy's two business decisions — a fixed 4-place scale and
/// banker's (to-even) midpoint rounding — so a future change to either is a deliberate,
/// test-breaking act rather than a silent drift in financial totals (design "Global column
/// and type conventions"; coding-standards §6).
/// </remarks>
public class MoneyRoundingTests
{
    [Fact]
    public void Policy_ExposesFourPlaceScaleAndBankersMode()
    {
        Assert.Equal(4, MoneyRounding.DecimalPlaces);
        Assert.Equal(MidpointRounding.ToEven, MoneyRounding.Mode);
    }

    [Theory]
    [InlineData("0", "0.0000")]
    [InlineData("1.5", "1.5000")]
    [InlineData("12.34567", "12.3457")]
    [InlineData("-12.34567", "-12.3457")]
    [InlineData("1000000.99999", "1000001.0000")]
    public void Round_ValueBeyondScale_RoundsToFourPlaces(string input, string expected)
    {
        decimal result = MoneyRounding.Round(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result);
    }

    [Theory]
    // Fifth decimal exactly at the midpoint: to-even keeps the even fourth digit.
    [InlineData("0.00005", "0.0000")] // rounds down to even 0
    [InlineData("0.00015", "0.0002")] // rounds up to even 2
    [InlineData("0.00025", "0.0002")] // rounds down to even 2
    [InlineData("0.00035", "0.0004")] // rounds up to even 4
    public void Round_ExactMidpoint_UsesBankersRounding(string input, string expected)
    {
        decimal result = MoneyRounding.Round(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void Round_AlreadyAtScale_IsUnchanged()
    {
        decimal value = 42.1234m;

        Assert.Equal(value, MoneyRounding.Round(value));
    }

    [Fact]
    public void Round_IsIdempotent()
    {
        decimal once = MoneyRounding.Round(9.876543m);
        decimal twice = MoneyRounding.Round(once);

        Assert.Equal(once, twice);
    }
}
