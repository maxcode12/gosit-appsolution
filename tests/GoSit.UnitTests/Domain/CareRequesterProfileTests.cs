using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.Common;
using GoSit.Domain.Common.ValueObjects;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class CareRequesterProfileTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static CareRequesterProfile CreateProfile() =>
        CareRequesterProfile.Create(Guid.NewGuid(), "416-555-0142", Now);

    [Fact]
    public void Create_normalizes_the_phone_number()
    {
        var profile = CareRequesterProfile.Create(Guid.NewGuid(), "(416) 555-0142", Now);

        Assert.Equal("+14165550142", profile.PhoneNumber);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("155-555-0142")]
    [InlineData("416-155-0142")]
    [InlineData("not a phone number")]
    public void Create_rejects_an_invalid_phone_number(string phoneNumber)
    {
        Assert.Throws<DomainException>(() =>
            CareRequesterProfile.Create(Guid.NewGuid(), phoneNumber, Now));
    }

    [Fact]
    public void A_child_can_be_added_as_a_care_recipient()
    {
        var profile = CreateProfile();

        var recipient = profile.AddCareRecipient(
            "Sam", "Okafor", new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);

        Assert.Equal("Sam", recipient.FirstName);
        Assert.Equal(8, recipient.AgeOn(Today));
        Assert.Single(profile.CareRecipients);
    }

    [Fact]
    public void A_senior_can_be_added_as_a_care_recipient()
    {
        var profile = CreateProfile();

        var recipient = profile.AddCareRecipient(
            "Miriam", null, new DateOnly(1944, 1, 9), CareRecipientType.Senior, Now);

        Assert.Equal(CareRecipientType.Senior, recipient.RecipientType);
        Assert.Equal(82, recipient.AgeOn(Today));
    }

    /// <summary>
    /// Caregiver matching keys off the recipient type, so a minor recorded as an adult
    /// or senior would surface caregivers who are not cleared to work with children.
    /// </summary>
    [Theory]
    [InlineData(CareRecipientType.Adult)]
    [InlineData(CareRecipientType.Senior)]
    public void A_minor_cannot_be_recorded_as_an_adult_or_senior(CareRecipientType type)
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() =>
            profile.AddCareRecipient("Sam", null, new DateOnly(2018, 4, 2), type, Now));
    }

    [Fact]
    public void An_adult_cannot_be_recorded_as_a_child()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() =>
            profile.AddCareRecipient("Dana", null, new DateOnly(1990, 6, 1), CareRecipientType.Child, Now));
    }

    /// <summary>
    /// Someone in their sixties may reasonably be recorded either way, so both are allowed.
    /// </summary>
    [Theory]
    [InlineData(CareRecipientType.Adult)]
    [InlineData(CareRecipientType.Senior)]
    public void An_older_adult_may_be_recorded_as_adult_or_senior(CareRecipientType type)
    {
        var profile = CreateProfile();

        var recipient = profile.AddCareRecipient(
            "Ines", null, new DateOnly(1960, 3, 3), type, Now);

        Assert.Equal(type, recipient.RecipientType);
    }

    [Fact]
    public void A_future_date_of_birth_is_rejected()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() =>
            profile.AddCareRecipient("Sam", null, Today.AddDays(1), CareRecipientType.Child, Now));
    }

    [Fact]
    public void Care_needs_start_empty_and_are_recorded_on_update()
    {
        var profile = CreateProfile();
        var recipient = profile.AddCareRecipient(
            "Sam", null, new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);

        Assert.True(recipient.CareNeeds.IsEmpty);

        profile.UpdateCareNeeds(recipient.Id, new CareNeedsDetails
        {
            Allergies = "Peanuts",
            FeedingAssistance = true,
            RequiredSkills = ["first_aid", "First_Aid", " cpr "],
        }, Now);

        Assert.False(recipient.CareNeeds.IsEmpty);
        Assert.Equal("Peanuts", recipient.CareNeeds.Allergies);
        Assert.True(recipient.CareNeeds.FeedingAssistance);

        // Skill codes are normalized and de-duplicated so eligibility checks can compare them directly.
        Assert.Equal(["FIRST_AID", "CPR"], recipient.CareNeeds.RequiredSkills);
    }

    [Fact]
    public void Updating_care_needs_replaces_rather_than_merges()
    {
        var profile = CreateProfile();
        var recipient = profile.AddCareRecipient(
            "Sam", null, new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);

        profile.UpdateCareNeeds(recipient.Id, new CareNeedsDetails { Allergies = "Peanuts" }, Now);
        profile.UpdateCareNeeds(recipient.Id, new CareNeedsDetails { Triggers = "Loud noises" }, Now);

        // A cleared allergy must actually clear — merging would silently retain stale
        // medical-adjacent information a requester believed they had removed.
        Assert.Null(recipient.CareNeeds.Allergies);
        Assert.Equal("Loud noises", recipient.CareNeeds.Triggers);
    }

    [Fact]
    public void Care_needs_reject_an_over_long_note()
    {
        var profile = CreateProfile();
        var recipient = profile.AddCareRecipient(
            "Sam", null, new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);

        Assert.Throws<DomainException>(() =>
            profile.UpdateCareNeeds(
                recipient.Id,
                new CareNeedsDetails { Triggers = new string('x', 2001) },
                Now));
    }

    [Fact]
    public void A_recipient_belonging_to_another_profile_is_not_found()
    {
        var mine = CreateProfile();
        var theirs = CreateProfile();
        var theirRecipient = theirs.AddCareRecipient(
            "Sam", null, new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);

        Assert.Null(mine.FindCareRecipient(theirRecipient.Id));
        Assert.Throws<DomainException>(() => mine.RemoveCareRecipient(theirRecipient.Id, Now));
    }

    [Fact]
    public void Removing_a_recipient_takes_their_care_needs_with_them()
    {
        var profile = CreateProfile();
        var recipient = profile.AddCareRecipient(
            "Sam", null, new DateOnly(2018, 4, 2), CareRecipientType.Child, Now);
        profile.UpdateCareNeeds(recipient.Id, new CareNeedsDetails { Allergies = "Peanuts" }, Now);

        profile.RemoveCareRecipient(recipient.Id, Now);

        Assert.Empty(profile.CareRecipients);
    }

    [Fact]
    public void Emergency_contacts_normalize_their_phone_number()
    {
        var profile = CreateProfile();

        var contact = profile.AddEmergencyContact("Ada Lovelace", "Aunt", "4165550188", Now);

        Assert.Equal("+14165550188", contact.PhoneNumber);
        Assert.Single(profile.EmergencyContacts);
    }

    [Fact]
    public void An_address_keeps_the_exact_value_but_can_be_reduced_for_display()
    {
        var profile = CreateProfile();
        var address = Address.Create("12 Bloor St W", "Unit 4", "Toronto", "on", "m5s 1m2");

        profile.SetAddress(address, Now);

        Assert.Equal("M5S1M2", profile.Address!.PostalCode);
        Assert.Equal("ON", profile.Address.Province);

        // Search and caregiver-facing views use this, never the street address.
        Assert.Equal("Toronto, ON M5S", profile.Address.ToApproximate());
    }

    [Theory]
    [InlineData("not-a-postal-code")]
    [InlineData("12345")]
    [InlineData("M5S1M")]
    public void An_invalid_postal_code_is_rejected(string postalCode)
    {
        Assert.Throws<DomainException>(() =>
            Address.Create("12 Bloor St W", null, "Toronto", "ON", postalCode));
    }
}
