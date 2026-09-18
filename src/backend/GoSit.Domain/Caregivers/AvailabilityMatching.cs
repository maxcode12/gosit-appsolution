namespace GoSit.Domain.Caregivers;

/// <summary>
/// Whether a caregiver's recurring weekly schedule covers a requested window.
///
/// Times are compared in the caregiver's local time zone, which for the Ontario-first
/// launch is the same zone the requester is in. Cross-zone bookings need explicit
/// conversion before this is reused elsewhere.
/// </summary>
public static class AvailabilityMatching
{
    /// <summary>
    /// True when a single slot fully contains the requested window.
    ///
    /// Adjacent slots are deliberately not stitched together: two windows that merely
    /// touch may represent separate commitments, so a request spanning both is not
    /// treated as covered.
    ///
    /// Both sides are compared as intervals on a circular week, so a window that runs
    /// past midnight — or past Saturday — is matched by a slot that does the same.
    /// </summary>
    public static bool Covers(
        IEnumerable<AvailabilitySlot> availability,
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        if (WeeklyInterval.Between(day, startTime, endTime) is not { } requested)
        {
            return false;
        }

        return availability.Any(slot => slot.Interval.Contains(requested));
    }

    /// <summary>
    /// The same question for a concrete booking, which unlike a weekly search filter
    /// carries dates and so can be longer than a day.
    /// </summary>
    public static bool Covers(
        IEnumerable<AvailabilitySlot> availability,
        DateTime localStart,
        DateTime localEnd)
    {
        if (localEnd <= localStart)
        {
            return false;
        }

        var requested = WeeklyInterval.ForBooking(localStart, localEnd);

        // A slot is at most twenty-four hours, so nothing longer can ever be covered by
        // one. Checked here so the answer is a plain no rather than an arithmetic
        // accident of the circular comparison.
        if (requested.LengthMinutes > WeeklyInterval.MinutesPerDay)
        {
            return false;
        }

        return availability.Any(slot => slot.Interval.Contains(requested));
    }
}
