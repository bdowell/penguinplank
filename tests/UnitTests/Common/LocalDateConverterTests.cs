using PenguinPlank.Domain.Common;

namespace PenguinPlank.UnitTests.Common;

public sealed class LocalDateConverterTests
{
    private static readonly TimeZoneInfo s_zone =
        TimeZoneInfo.FindSystemTimeZoneById(LocalDateConverter.BusinessTimeZoneId);

    [Theory]
    [InlineData("2025-03-08", -8)]
    [InlineData("2025-03-09", -8)] // spring-forward day: midnight is still standard time
    [InlineData("2025-03-10", -7)]
    [InlineData("2025-11-02", -7)] // fall-back day: midnight is still daylight time
    [InlineData("2025-11-03", -8)]
    public void ToStoredValue_DateAroundDst_UsesZoneOffsetForThatDate(string date, int expectedHours)
    {
        var stored = LocalDateConverter.ToStoredValue(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), s_zone);

        Assert.Equal(TimeSpan.FromHours(expectedHours), stored.Offset);
        Assert.Equal(TimeSpan.Zero, stored.TimeOfDay);
    }

    [Fact]
    public void ToLocalDate_StoredValueRoundTrippedThroughUtc_ReturnsSameDate()
    {
        var date = new DateOnly(2025, 3, 9);
        var stored = LocalDateConverter.ToStoredValue(date, s_zone);

        var resolved = LocalDateConverter.ToLocalDate(stored.ToUniversalTime(), s_zone);

        Assert.Equal(date, resolved);
    }

    [Fact]
    public void GetToday_LateEveningPacific_ReturnsPacificDateNotUtcDate()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2025, 7, 5, 5, 30, 0, TimeSpan.Zero));

        var today = LocalDateConverter.GetToday(provider, s_zone);

        Assert.Equal(new DateOnly(2025, 7, 4), today);
    }

    [Fact]
    public void ToApiTimestamp_UtcInstantInSummer_CarriesPacificDaylightOffset()
    {
        var instant = new DateTimeOffset(2025, 7, 1, 19, 0, 0, TimeSpan.Zero);

        var result = LocalDateConverter.ToApiTimestamp(instant, s_zone);

        Assert.Equal(TimeSpan.FromHours(-7), result.Offset);
        Assert.Equal(instant, result);
    }

    [Fact]
    public void ToStoredValue_NullZone_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LocalDateConverter.ToStoredValue(new DateOnly(2025, 1, 1), null!));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
