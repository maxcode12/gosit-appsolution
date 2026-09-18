using GoSit.Domain.Bookings;
using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using GoSit.Domain.Pets;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// Pet care, and the screening line it draws through the caregiver pool.
///
/// The property that matters most is not that pets can be booked — it is that a caregiver
/// cleared only for pets can never be booked for a person, whatever services their profile
/// lists. Verification used to have one bar because every service was care for a person;
/// splitting it is only safe if every eligibility check knows about the split.
/// </summary>
public class PetCareTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static CareService Babysitting() =>
        CareService.Create(Guid.NewGuid(), "BABYSITTING", "Babysitting", null, [], [CareRecipientType.Child]);

    private static CareService DogWalking() =>
        CareService.Create(Guid.NewGuid(), "DOG_WALKING", "Dog Walking", null, [], [], [PetSpecies.Dog]);

    private static CaregiverProfile CaregiverOffering(params CareService[] services)
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);
        profile.ReplaceServices(services, Now);
        return profile;
    }

    [Fact]
    public void A_service_is_for_people_or_for_pets_never_both()
    {
        // Screening, booking and disclosure all branch on which kind of care a service is.
        Assert.Throws<DomainException>(() => CareService.Create(
            Guid.NewGuid(), "MIXED", "Mixed", null, [], [CareRecipientType.Child], [PetSpecies.Dog]));
    }

    [Fact]
    public void A_service_must_be_for_someone_or_something()
    {
        Assert.Throws<DomainException>(() =>
            CareService.Create(Guid.NewGuid(), "EMPTY", "Empty", null, [], [], []));
    }

    [Fact]
    public void A_pet_service_applies_only_to_the_species_it_names()
    {
        var walking = DogWalking();

        Assert.True(walking.IsPetCare);
        Assert.True(walking.AppliesTo(PetSpecies.Dog));
        Assert.False(walking.AppliesTo(PetSpecies.Cat));
        Assert.False(Babysitting().IsPetCare);
    }

    /// <summary>
    /// The rule the whole split rests on. A pet sitter who adds Babysitting to their profile
    /// has made a claim; their checks do not back it.
    /// </summary>
    [Fact]
    public void A_caregiver_cleared_only_for_pets_is_not_eligible_for_care_for_people()
    {
        var babysitting = Babysitting();
        var walking = DogWalking();
        var profile = CaregiverOffering(babysitting, walking);

        profile.MarkVerified(clearedForPeopleCare: false, Now);

        Assert.True(profile.IsEligibleFor(walking, Today));
        Assert.False(profile.IsEligibleFor(babysitting, Today));
    }

    /// <summary>A vulnerable sector check includes a criminal record check, so people clearance covers pets too.</summary>
    [Fact]
    public void A_caregiver_cleared_for_people_is_eligible_for_pet_care_as_well()
    {
        var babysitting = Babysitting();
        var walking = DogWalking();
        var profile = CaregiverOffering(babysitting, walking);

        profile.MarkVerified(clearedForPeopleCare: true, Now);

        Assert.True(profile.IsEligibleFor(walking, Today));
        Assert.True(profile.IsEligibleFor(babysitting, Today));
    }

    /// <summary>
    /// Offering nothing yet is held to the people-care bar: before we know what someone will
    /// offer, the stricter answer is the safe one.
    /// </summary>
    [Fact]
    public void Only_a_caregiver_offering_nothing_but_pet_care_needs_the_pet_bar()
    {
        Assert.True(CaregiverOffering(DogWalking()).OffersOnlyPetCare);
        Assert.False(CaregiverOffering(DogWalking(), Babysitting()).OffersOnlyPetCare);
        Assert.False(CaregiverOffering().OffersOnlyPetCare);
    }

    [Fact]
    public void Rejection_withdraws_clearance_for_people()
    {
        var profile = CaregiverOffering(Babysitting());

        profile.MarkVerified(clearedForPeopleCare: true, Now);
        profile.MarkVerificationRejected(Now);

        Assert.False(profile.ClearedForPeopleCare);
    }

    /// <summary>A dog walk is commonly half an hour; the one-hour floor would make it unbookable.</summary>
    [Fact]
    public void Pet_care_can_be_booked_for_half_an_hour_and_care_for_people_cannot()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), BookingPolicy.MinimumDurationFor(DogWalking()));
        Assert.Equal(TimeSpan.FromHours(1), BookingPolicy.MinimumDurationFor(Babysitting()));
    }
}
