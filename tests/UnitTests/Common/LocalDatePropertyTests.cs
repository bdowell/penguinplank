using System.Globalization;
using CsCheck;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.UnitTests.Common;

/// <summary>
/// Property 26 (requirement 10.2): <em>Date-only local-date preservation across DST.</em> For any
/// intended local date, storing it with <see cref="LocalDateConverter.ToStoredValue"/> and
/// resolving it with <see cref="LocalDateConverter.ToLocalDate"/> in the America/Los_Angeles
/// business timezone returns the same date, including on and around spring-forward and fall-back
/// days, and after the stored value is re-expressed in UTC, in arbitrary offsets, or serialized.
/// </summary>
/// <remarks>
/// The functions are pure and take the zone explicitly, so no database, clock or application
/// startup is needed. The generator mixes arbitrary dates with the DST transition dates (second
/// Sunday of March, first Sunday of November) and their neighbours.
/// </remarks>
public sealed class LocalDatePropertyTests
{
    /// <summary>Iterations per property; the design requires at least 100.</summary>
    private const int Iterations = 1000;

    private const int FirstYear = 2007;
    private const int LastYear = 2060;

    private static readonly TimeZoneInfo s_zone =
        TimeZoneInfo.FindSystemTimeZoneById(LocalDateConverter.BusinessTimeZoneId);

    /// <summary>Generates any calendar date in the supported year range.</summary>
    private static readonly Gen<DateOnly> s_genAnyDate =
        Gen.Int[new DateOnly(FirstYear, 1, 1).DayNumber, new DateOnly(LastYear, 12, 31).DayNumber]
            .Select(DateOnly.FromDayNumber);

    /// <summary>Generates a date within three days of a DST transition day.</summary>
    private static readonly Gen<DateOnly> s_genNearDstTransition =
        Gen.Select(Gen.Int[FirstYear, LastYear], Gen.Bool, Gen.Int[-3, 3])
            .Select((year, isSpring, dayShift) =>
                (isSpring ? NthSunday(year, 3, 2) : NthSunday(year, 11, 1)).AddDays(dayShift));

    private static readonly Gen<DateOnly> s_genDate =
        Gen.Frequency((1, s_genAnyDate), (1, s_genNearDstTransition));

    /// <summary>Generates a whole-minute offset within the valid DateTimeOffset range (±14h).</summary>
    private static readonly Gen<TimeSpan> s_genOffset =
        Gen.Int[-14 * 60, 14 * 60].Select(minutes => TimeSpan.FromMinutes(minutes));

    [Fact]
    public void ToLocalDate_StoredLocalDate_ReturnsSameDate()
    {
        s_genDate.Sample(
            date =>
            {
                var stored = LocalDateConverter.ToStoredValue(date, s_zone);

                Assert.Equal(date, LocalDateConverter.ToLocalDate(stored, s_zone));
            },
            iter: Iterations);
    }

    [Fact]
    public void ToLocalDate_StoredValueReexpressedInAnyOffset_ReturnsSameDate()
    {
        Gen.Select(s_genDate, s_genOffset).Sample(
            (date, offset) =>
            {
                var stored = LocalDateConverter.ToStoredValue(date, s_zone);
                var reexpressed = stored.ToOffset(offset);

                Assert.Equal(date, LocalDateConverter.ToLocalDate(reexpressed, s_zone));
            },
            iter: Iterations);
    }

    [Fact]
    public void ToLocalDate_StoredValueRoundTrippedThroughUtcTicks_ReturnsSameDate()
    {
        s_genDate.Sample(
            date =>
            {
                var stored = LocalDateConverter.ToStoredValue(date, s_zone);
                var restored = new DateTimeOffset(stored.UtcTicks, TimeSpan.Zero);

                Assert.Equal(date, LocalDateConverter.ToLocalDate(restored, s_zone));
            },
            iter: Iterations);
    }

    [Fact]
    public void ToLocalDate_StoredValueSerializedAsRoundTripText_ReturnsSameDate()
    {
        s_genDate.Sample(
            date =>
            {
                var stored = LocalDateConverter.ToStoredValue(date, s_zone);
                string text = stored.ToString("O", CultureInfo.InvariantCulture);
                var parsed = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);

                Assert.Equal(date, LocalDateConverter.ToLocalDate(parsed, s_zone));
            },
            iter: Iterations);
    }

    [Fact]
    public void ToStoredValue_AnyDate_IsStartOfLocalDayWithZoneOffsetForThatDate()
    {
        s_genDate.Sample(
            date =>
            {
                var stored = LocalDateConverter.ToStoredValue(date, s_zone);

                Assert.Equal(TimeSpan.Zero, stored.TimeOfDay);
                Assert.Equal(s_zone.GetUtcOffset(stored.DateTime), stored.Offset);
            },
            iter: Iterations);
    }

    private static DateOnly NthSunday(int year, int month, int n)
    {
        var first = new DateOnly(year, month, 1);
        int daysToSunday = ((int)DayOfWeek.Sunday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(daysToSunday + ((n - 1) * 7));
    }
}
