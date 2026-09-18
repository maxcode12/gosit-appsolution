using GoSit.Domain.Bookings;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using GoSit.Domain.Pets;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// Pet boarding: care at the caregiver's home, priced per night.
/// </summary>
public class BoardingTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static CareService Boarding() =>
        CareService.Create(
            Guid.NewGuid(), "PET_BOARDING", "Pet Boarding", null, [], [],
            [PetSpecies.Dog, PetSpecies.Cat], PricingUnit.Nightly, CareLocation.CaregiverHome);

    /// <summary>
    /// Care for people at a caregiver's own home is what Ontario licenses as home child
    /// care. A service must not be able to describe it, even by a configuration slip.
    /// </summary>
    [Fact]
    public void Care_for_people_cannot_happen_at_a_caregivers_home()
    {
        Assert.Throws<DomainException>(() => CareService.Create(
            Guid.NewGuid(), "HOME_DAYCARE", "Home Daycare", null, [], [CareRecipientType.Child],
            null, PricingUnit.Hourly, CareLocation.CaregiverHome));
    }

    [Fact]
    public void Boarding_is_priced_per_night_at_the_caregivers_home()
    {
        var boarding = Boarding();

        Assert.Equal(PricingUnit.Nightly, boarding.PricingUnit);
        Assert.Equal(CareLocation.CaregiverHome, boarding.CareLocation);
        Assert.True(boarding.IsPetCare);
    }

    /// <summary>A service priced by the night cannot be offered by someone with no nightly price.</summary>
    [Fact]
    public void A_caregiver_needs_a_nightly_rate_to_be_eligible_for_boarding()
    {
        var boarding = Boarding();

        var profile = CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);
        profile.ReplaceServices([boarding], Now);
        profile.MarkVerified(clearedForPeopleCare: false, Now);

        Assert.False(profile.IsEligibleFor(boarding, Today));

        profile.SetNightlyRate(45m, Now);

        Assert.True(profile.IsEligibleFor(boarding, Today));
    }

    [Fact]
    public void A_nightly_rate_can_be_cleared_by_someone_who_stops_boarding()
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);

        profile.SetNightlyRate(45m, Now);
        profile.SetNightlyRate(null, Now);

        Assert.Null(profile.NightlyRate);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(600)]
    public void An_implausible_nightly_rate_is_refused(decimal nightlyRate)
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);

        Assert.Throws<DomainException>(() => profile.SetNightlyRate(nightlyRate, Now));
    }

    /// <summary>
    /// Nights are counted in local dates. Friday 10pm to Sunday 10am in Toronto is two
    /// nights, though in UTC the drop-off has already become Saturday and it would count one.
    /// </summary>
    [Fact]
    public void Nights_are_counted_by_local_dates_not_utc_ones()
    {
        var fridayTenPm = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var sundayTenAm = new DateTime(2026, 9, 13, 14, 0, 0, DateTimeKind.Utc);

        Assert.Equal(2, BookingPolicy.NightsBetween(fridayTenPm, sundayTenAm));
    }

    [Fact]
    public void A_same_day_drop_off_and_pick_up_is_no_nights()
    {
        var morning = new DateTime(2026, 9, 11, 13, 0, 0, DateTimeKind.Utc);

        Assert.Equal(0, BookingPolicy.NightsBetween(morning, morning.AddHours(6)));
    }
}
