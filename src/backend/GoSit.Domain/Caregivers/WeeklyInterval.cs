namespace GoSit.Domain.Caregivers;

/// <summary>
/// A stretch of the week, as a starting minute and a length.
///
/// The week is treated as a circle rather than seven separate days, which is the whole
/// point of the type. A caregiver working Friday 8pm to Saturday 7am is doing one shift,
/// not two, and the previous model — a start and end time pinned to a single day — could
/// not say so. That was not a cosmetic limitation: GoSit sells "Overnight Child Care",
/// and every attempt to book it was refused with "this caregiver is not available at that
/// time", which blamed the caregiver for a gap in our own model.
///
/// Circular arithmetic also handles the Saturday-to-Sunday case for free, which a
/// day-plus-wrap-flag representation would have needed a special case for and eventually
/// got wrong.
/// </summary>
public readonly record struct WeeklyInterval(int StartMinute, int LengthMinutes)
{
    public const int MinutesPerDay = 24 * 60;

    public const int MinutesPerWeek = 7 * MinutesPerDay;

    /// <summary>Where in the week a day-and-time falls.</summary>
    public static int MinuteOf(DayOfWeek day, TimeOnly time) =>
        ((int)day * MinutesPerDay) + (time.Hour * 60) + time.Minute;

    /// <summary>
    /// The interval from one weekly time to another, wrapping past midnight — and past
    /// Saturday — when the end is not after the start.
    ///
    /// Equal times are refused rather than read as a full day. "Available Monday 09:00 to
    /// 09:00" is at least as likely to be a typo as a request to work twenty-four hours,
    /// and guessing wrong in either direction is worse than asking.
    /// </summary>
    public static WeeklyInterval? Between(DayOfWeek day, TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime == endTime)
        {
            return null;
        }

        var start = MinuteOf(day, startTime);
        var minutesInDay = (time: endTime, from: startTime);

        var length = ((minutesInDay.time.Hour * 60) + minutesInDay.time.Minute)
            - ((minutesInDay.from.Hour * 60) + minutesInDay.from.Minute);

        if (length < 0)
        {
            length += MinutesPerDay;
        }

        return new WeeklyInterval(start, length);
    }

    /// <summary>The interval a booking occupies, in the caregiver's local time.</summary>
    public static WeeklyInterval ForBooking(DateTime localStart, DateTime localEnd) =>
        new(
            MinuteOf(localStart.DayOfWeek, TimeOnly.FromDateTime(localStart)),
            (int)Math.Ceiling((localEnd - localStart).TotalMinutes));

    /// <summary>
    /// Whether this interval wholly contains another.
    ///
    /// Measured as an offset around the circle, so a slot that wraps past midnight covers
    /// a booking that wraps with it without either needing to know it is wrapping.
    /// </summary>
    public bool Contains(WeeklyInterval other)
    {
        if (other.LengthMinutes <= 0 || other.LengthMinutes > LengthMinutes)
        {
            return false;
        }

        var offset = Modulo(other.StartMinute - StartMinute);

        return offset + other.LengthMinutes <= LengthMinutes;
    }

    /// <summary>
    /// Whether two intervals share any minute.
    ///
    /// Each is checked against the other because containment is not symmetric on a
    /// circle: a long interval can swallow a short one whose own start is far behind it.
    /// </summary>
    public bool Overlaps(WeeklyInterval other) =>
        Modulo(other.StartMinute - StartMinute) < LengthMinutes
        || Modulo(StartMinute - other.StartMinute) < other.LengthMinutes;

    /// <summary>Always non-negative, unlike the remainder operator.</summary>
    private static int Modulo(int value) =>
        ((value % MinutesPerWeek) + MinutesPerWeek) % MinutesPerWeek;
}
