namespace PenguinPlank.Domain.Common;

/// <summary>
/// Pure functions that preserve the intended local calendar date of date-only fields (event
/// start/end dates, follow-up due dates) across storage and resolution, and that format API
/// timestamps with offsets (requirements 10.1, 10.2, 10.3).
/// </summary>
/// <remarks>
/// <para>
/// A date-only field is persisted as a <see cref="DateTimeOffset"/> anchored at the start of the
/// intended local day in the business timezone, carrying that zone's offset for that date
/// (-08:00 in standard time, -07:00 in daylight time). Resolution converts the stored value back
/// into the same zone and takes its calendar date, so the intended date survives a UTC round trip
/// and DST transitions. Using the zone's own offset for that date means the stored instant never
/// drifts onto a neighbouring local day.
/// </para>
/// <para>
/// If local midnight does not exist on a date (a zone whose DST gap begins at midnight), the
/// anchor is the first valid instant of that local day. The business zone
/// <see cref="BusinessTimeZoneId"/> has its gap at 02:00, so this is a defensive rule.
/// </para>
/// All functions take the zone and instants explicitly; none read the system clock. Callers
/// obtain "now" from an injected <see cref="TimeProvider"/>.
/// </remarks>
public static class LocalDateConverter
{
    /// <summary>The IANA identifier of the business timezone.</summary>
    public const string BusinessTimeZoneId = "America/Los_Angeles";

    /// <summary>
    /// Converts an intended local date to the <see cref="DateTimeOffset"/> stored for it: the
    /// start of that local day in <paramref name="zone"/>, with the zone's offset for that day.
    /// </summary>
    /// <param name="localDate">The intended local calendar date.</param>
    /// <param name="zone">The timezone the date is interpreted in.</param>
    /// <returns>The anchored date-time with the zone's offset for that date.</returns>
    public static DateTimeOffset ToStoredValue(DateOnly localDate, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var localMidnight = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Walk forward past a DST gap so the anchor is a real local time.
        while (zone.IsInvalidTime(localMidnight))
        {
            localMidnight = localMidnight.AddMinutes(1);
        }

        var offset = zone.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset);
    }

    /// <summary>
    /// Resolves a stored date-only value back to its intended local date in
    /// <paramref name="zone"/>.
    /// </summary>
    /// <param name="storedValue">The stored value (any offset; it is converted to the zone).</param>
    /// <param name="zone">The timezone the date is interpreted in.</param>
    /// <returns>The intended local calendar date.</returns>
    public static DateOnly ToLocalDate(DateTimeOffset storedValue, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var local = TimeZoneInfo.ConvertTime(storedValue, zone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    /// <summary>
    /// Gets today's local date in <paramref name="zone"/> from the injected time source.
    /// </summary>
    /// <param name="timeProvider">The time source.</param>
    /// <param name="zone">The timezone whose calendar date is wanted.</param>
    /// <returns>The current local calendar date in the zone.</returns>
    public static DateOnly GetToday(TimeProvider timeProvider, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(zone);

        return ToLocalDate(timeProvider.GetUtcNow(), zone);
    }

    /// <summary>
    /// Converts an instant to <paramref name="zone"/> so it is serialized with that zone's
    /// offset (for example <c>2025-03-09T03:00:00-07:00</c>) rather than as an offset-less value.
    /// </summary>
    /// <param name="instant">The instant to present.</param>
    /// <param name="zone">The presentation timezone.</param>
    /// <returns>The same instant expressed with the zone's offset.</returns>
    public static DateTimeOffset ToApiTimestamp(DateTimeOffset instant, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return TimeZoneInfo.ConvertTime(instant, zone);
    }
}
