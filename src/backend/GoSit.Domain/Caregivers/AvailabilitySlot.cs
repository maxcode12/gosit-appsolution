using GoSit.Domain.Common;

namespace GoSit.Domain.Caregivers;

/// <summary>
/// A recurring weekly window in which a caregiver is willing to work, stored in the
/// caregiver local time zone. Per-booking exceptions arrive later with CareBooking.
///
/// An end time at or before the start means the window runs past midnight into the next
/// day: Friday 20:00–07:00 is one shift ending Saturday morning. That is what makes
/// overnight care expressible at all — see <see cref="WeeklyInterval"/>.
/// </summary>
public sealed class AvailabilitySlot : Entity
{
    private AvailabilitySlot()
    {
    }

    public Guid CaregiverProfileId { get; private set; }

    /// <summary>The day the window <em>starts</em>. It may end on the next one.</summary>
    public DayOfWeek Day { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    /// <summary>True when this window runs past midnight.</summary>
    public bool CrossesMidnight => EndTime <= StartTime;

    /// <summary>Where this sits in the week, as a circular interval.</summary>
    public WeeklyInterval Interval =>
        WeeklyInterval.Between(Day, StartTime, EndTime)
        // Create refuses equal times, so this cannot be null on a persisted slot.
        ?? throw new DomainException("This availability window has no duration.");

    /// <summary>
    /// Whether two windows share any minute of the week.
    ///
    /// Compared across the whole week rather than within a day, because a window that
    /// runs past midnight collides with the next morning's — Friday 20:00–07:00 and
    /// Saturday 06:00–10:00 are an hour of double-booking that the old same-day check
    /// could not see.
    /// </summary>
    public bool OverlapsWith(AvailabilitySlot other) => Interval.Overlaps(other.Interval);

    internal static AvailabilitySlot Create(
        Guid caregiverProfileId,
        DayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        if (startTime == endTime)
        {
            // Ambiguous rather than invalid: it could mean a full day or nothing at all.
            // A caregiver who genuinely works round the clock enters two windows.
            throw new DomainException(
                "An availability window must have a start and end that differ. "
                + "To work past midnight, set an end time earlier in the day than the start.");
        }

        return new AvailabilitySlot
        {
            CaregiverProfileId = caregiverProfileId,
            Day = day,
            StartTime = startTime,
            EndTime = endTime,
        };
    }
}
