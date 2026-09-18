using GoSit.Domain.Caregivers;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// The circular-week arithmetic behind overnight availability.
///
/// Worth testing directly rather than only through a booking, because the failure modes
/// here are silent and asymmetric: an interval that wraps the wrong way does not throw,
/// it quietly makes a caregiver available at three in the morning on a night they never
/// agreed to work.
/// </summary>
public class WeeklyIntervalTests
{
    private static WeeklyInterval Slot(DayOfWeek day, int startHour, int endHour) =>
        WeeklyInterval.Between(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0))!.Value;

    [Fact]
    public void A_window_within_one_day_has_the_obvious_length()
    {
        Assert.Equal(8 * 60, Slot(DayOfWeek.Monday, 9, 17).LengthMinutes);
    }

    [Fact]
    public void A_window_ending_earlier_than_it_starts_wraps_past_midnight()
    {
        // Friday 20:00 to Saturday 07:00 is eleven hours, not minus thirteen.
        Assert.Equal(11 * 60, Slot(DayOfWeek.Friday, 20, 7).LengthMinutes);
    }

    [Fact]
    public void Equal_times_have_no_interval()
    {
        Assert.Null(WeeklyInterval.Between(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void An_overnight_slot_contains_an_overnight_booking()
    {
        var slot = Slot(DayOfWeek.Friday, 19, 8);
        var booking = Slot(DayOfWeek.Friday, 21, 6);

        Assert.True(slot.Contains(booking));
    }

    /// <summary>
    /// The failure that would put a stranger in a house at 2am. A daytime slot must not
    /// wrap round to cover the small hours.
    /// </summary>
    [Fact]
    public void A_daytime_slot_does_not_contain_an_overnight_booking()
    {
        var slot = Slot(DayOfWeek.Friday, 9, 17);
        var booking = Slot(DayOfWeek.Friday, 21, 6);

        Assert.False(slot.Contains(booking));
    }

    [Fact]
    public void A_booking_running_past_the_end_of_a_slot_is_not_contained()
    {
        var slot = Slot(DayOfWeek.Friday, 19, 8);

        // Ends at noon; the caregiver finishes at eight.
        Assert.False(slot.Contains(Slot(DayOfWeek.Friday, 20, 12)));
    }

    /// <summary>
    /// The week is a circle, so a Saturday-night shift runs into Sunday morning rather
    /// than falling off the end of the array.
    /// </summary>
    [Fact]
    public void A_saturday_night_slot_covers_sunday_morning()
    {
        var slot = Slot(DayOfWeek.Saturday, 22, 6);

        Assert.Equal(8 * 60, slot.LengthMinutes);
        Assert.True(slot.Contains(Slot(DayOfWeek.Saturday, 23, 5)));
    }

    [Fact]
    public void Overlap_is_symmetric()
    {
        var friday = Slot(DayOfWeek.Friday, 19, 8);
        var saturdayMorning = Slot(DayOfWeek.Saturday, 6, 10);

        // The old same-day check could not see this hour of double-booking.
        Assert.True(friday.Overlaps(saturdayMorning));
        Assert.True(saturdayMorning.Overlaps(friday));
    }

    [Fact]
    public void Windows_that_merely_touch_do_not_overlap()
    {
        var morning = Slot(DayOfWeek.Monday, 9, 13);
        var afternoon = Slot(DayOfWeek.Monday, 13, 17);

        Assert.False(morning.Overlaps(afternoon));
        Assert.False(afternoon.Overlaps(morning));
    }

    /// <summary>
    /// A booking is measured from real timestamps, so it can be longer than a day even
    /// though a slot cannot. Nothing longer than a slot may be contained by one.
    /// </summary>
    [Fact]
    public void A_booking_longer_than_its_slot_is_not_contained()
    {
        var slot = Slot(DayOfWeek.Friday, 19, 8);

        var start = new DateTime(2026, 9, 4, 19, 0, 0, DateTimeKind.Unspecified);
        var booking = WeeklyInterval.ForBooking(start, start.AddHours(20));

        Assert.False(slot.Contains(booking));
    }
}
