using GoSit.Domain.Caregivers;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class AvailabilityMatchingTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyCollection<AvailabilitySlot> Schedule(
        params (DayOfWeek Day, string Start, string End)[] windows)
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);

        profile.ReplaceAvailability(
            windows.Select(w => new AvailabilityWindow(
                w.Day, TimeOnly.Parse(w.Start), TimeOnly.Parse(w.End))),
            Now);

        return profile.Availability;
    }

    [Fact]
    public void A_window_inside_a_slot_is_covered()
    {
        var availability = Schedule((DayOfWeek.Monday, "09:00", "17:00"));

        Assert.True(AvailabilityMatching.Covers(
            availability, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void A_window_exactly_matching_a_slot_is_covered()
    {
        var availability = Schedule((DayOfWeek.Monday, "09:00", "17:00"));

        Assert.True(AvailabilityMatching.Covers(
            availability, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
    }

    [Fact]
    public void A_window_extending_past_the_slot_is_not_covered()
    {
        var availability = Schedule((DayOfWeek.Monday, "09:00", "17:00"));

        Assert.False(AvailabilityMatching.Covers(
            availability, DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(18, 0)));
    }

    [Fact]
    public void A_window_on_a_different_day_is_not_covered()
    {
        var availability = Schedule((DayOfWeek.Monday, "09:00", "17:00"));

        Assert.False(AvailabilityMatching.Covers(
            availability, DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(12, 0)));
    }

    /// <summary>
    /// Two touching windows may be separate commitments, so a request spanning both is
    /// deliberately not treated as covered.
    /// </summary>
    [Fact]
    public void Adjacent_slots_are_not_stitched_together()
    {
        var availability = Schedule(
            (DayOfWeek.Monday, "09:00", "12:00"),
            (DayOfWeek.Monday, "12:00", "17:00"));

        Assert.False(AvailabilityMatching.Covers(
            availability, DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(13, 0)));
    }

    [Fact]
    public void The_correct_slot_is_found_among_several()
    {
        var availability = Schedule(
            (DayOfWeek.Monday, "09:00", "12:00"),
            (DayOfWeek.Wednesday, "13:00", "20:00"),
            (DayOfWeek.Friday, "08:00", "10:00"));

        Assert.True(AvailabilityMatching.Covers(
            availability, DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(19, 0)));
    }

    [Fact]
    public void An_inverted_window_is_never_covered()
    {
        var availability = Schedule((DayOfWeek.Monday, "09:00", "17:00"));

        Assert.False(AvailabilityMatching.Covers(
            availability, DayOfWeek.Monday, new TimeOnly(15, 0), new TimeOnly(11, 0)));
    }

    [Fact]
    public void An_empty_schedule_covers_nothing()
    {
        Assert.False(AvailabilityMatching.Covers(
            [], DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(12, 0)));
    }
}
