using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class CareServiceEligibilityTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);
    private static readonly Guid CategoryId = Guid.NewGuid();

    private static CareService OvernightChildCare() => CareService.Create(
        CategoryId,
        "OVERNIGHT_CHILD_CARE",
        "Overnight Child Care",
        null,
        ["VULNERABLE_SECTOR", "FIRST_AID", "CPR"],
        [CareRecipientType.Child]);

    private static CareService SeniorCompanionship() => CareService.Create(
        CategoryId, "SENIOR_COMPANIONSHIP", "Senior Companionship", null,
        ["POLICE_CHECK"], [CareRecipientType.Senior]);

    private static Certification Cert(string code, bool requiresExpiry = true) =>
        Certification.Create(code, code, requiresExpiry);

    /// <summary>
    /// Cleared for people by their checks, so these tests isolate what certificates add.
    /// Eligibility for care for people also needs that clearance — see PetCareTests.
    /// </summary>
    private static CaregiverProfile CreateCaregiver()
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);
        profile.MarkVerified(clearedForPeopleCare: true, Now);
        return profile;
    }

    private static void Approve(CaregiverProfile profile, params string[] codes)
    {
        foreach (var code in codes)
        {
            var submitted = profile.SubmitCertification(
                Cert(code), new DateOnly(2026, 1, 10), new DateOnly(2028, 1, 10), Now);
            // Approval now requires evidence — see CaregiverCertification.Approve.
            profile.AttachCertificationDocument(submitted.Id, "test/document", Now);
            profile.ApproveCertification(submitted.Id, Now);
        }
    }

    [Fact]
    public void A_service_records_its_certification_requirements_normalized()
    {
        var service = CareService.Create(
            CategoryId, "babysitting", "Babysitting", null,
            [" vulnerable_sector ", "VULNERABLE_SECTOR"], [CareRecipientType.Child]);

        Assert.Equal("BABYSITTING", service.Code);
        Assert.Equal(["VULNERABLE_SECTOR"], service.RequiredCertificationCodes);
    }

    [Fact]
    public void A_service_must_apply_to_at_least_one_recipient_type()
    {
        Assert.Throws<DomainException>(() =>
            CareService.Create(CategoryId, "X", "X", null, [], []));
    }

    [Fact]
    public void A_service_knows_which_recipients_it_applies_to()
    {
        var service = SeniorCompanionship();

        Assert.True(service.AppliesTo(CareRecipientType.Senior));
        Assert.False(service.AppliesTo(CareRecipientType.Child));
    }

    /// <summary>
    /// Offering a service is a claim. Eligibility additionally requires the
    /// certifications, so a caregiver cannot self-declare their way into a service.
    /// </summary>
    [Fact]
    public void Offering_a_service_without_the_certifications_does_not_make_a_caregiver_eligible()
    {
        var service = OvernightChildCare();
        var caregiver = CreateCaregiver();
        caregiver.ReplaceServices([service], Now);

        Assert.Contains(service, caregiver.Services);
        Assert.False(caregiver.IsEligibleFor(service, Today));
    }

    [Fact]
    public void Holding_the_certifications_without_offering_the_service_is_also_not_enough()
    {
        var service = OvernightChildCare();
        var caregiver = CreateCaregiver();
        Approve(caregiver, "VULNERABLE_SECTOR", "FIRST_AID", "CPR");

        Assert.False(caregiver.IsEligibleFor(service, Today));
    }

    [Fact]
    public void Offering_the_service_and_holding_every_certification_makes_a_caregiver_eligible()
    {
        var service = OvernightChildCare();
        var caregiver = CreateCaregiver();
        caregiver.ReplaceServices([service], Now);
        Approve(caregiver, "VULNERABLE_SECTOR", "FIRST_AID", "CPR");

        Assert.True(caregiver.IsEligibleFor(service, Today));
    }

    [Fact]
    public void A_partially_certified_caregiver_is_not_eligible()
    {
        var service = OvernightChildCare();
        var caregiver = CreateCaregiver();
        caregiver.ReplaceServices([service], Now);
        Approve(caregiver, "VULNERABLE_SECTOR", "FIRST_AID");

        Assert.False(caregiver.IsEligibleFor(service, Today));
    }

    /// <summary>
    /// Eligibility is evaluated on a date, so a caregiver who was eligible last year is
    /// not automatically eligible today.
    /// </summary>
    [Fact]
    public void Eligibility_lapses_when_a_required_certification_expires()
    {
        var service = SeniorCompanionship();
        var caregiver = CreateCaregiver();
        caregiver.ReplaceServices([service], Now);

        var submitted = caregiver.SubmitCertification(
            Cert("POLICE_CHECK"), new DateOnly(2026, 1, 10), new DateOnly(2026, 12, 31), Now);
        caregiver.AttachCertificationDocument(submitted.Id, "test/document", Now);
        caregiver.ApproveCertification(submitted.Id, Now);

        Assert.True(caregiver.IsEligibleFor(service, Today));
        Assert.False(caregiver.IsEligibleFor(service, new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void A_pending_certification_does_not_confer_eligibility()
    {
        var service = SeniorCompanionship();
        var caregiver = CreateCaregiver();
        caregiver.ReplaceServices([service], Now);
        caregiver.SubmitCertification(
            Cert("POLICE_CHECK"), new DateOnly(2026, 1, 10), new DateOnly(2028, 1, 10), Now);

        Assert.False(caregiver.IsEligibleFor(service, Today));
    }

    [Fact]
    public void Replacing_services_discards_the_previous_set()
    {
        var caregiver = CreateCaregiver();
        var overnight = OvernightChildCare();
        var senior = SeniorCompanionship();

        caregiver.ReplaceServices([overnight], Now);
        caregiver.ReplaceServices([senior], Now);

        Assert.Equal("SENIOR_COMPANIONSHIP", caregiver.Services.Single().Code);
    }

    [Fact]
    public void Duplicate_services_are_collapsed()
    {
        var caregiver = CreateCaregiver();
        var service = OvernightChildCare();

        caregiver.ReplaceServices([service, service], Now);

        Assert.Single(caregiver.Services);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5.1)]
    [InlineData(6)]
    public void A_rating_outside_one_to_five_is_rejected(decimal rating)
    {
        var caregiver = CreateCaregiver();

        Assert.Throws<DomainException>(() => caregiver.UpdateRatingSummary(rating, 3, Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void The_rating_bounds_themselves_are_accepted(decimal rating)
    {
        var caregiver = CreateCaregiver();

        caregiver.UpdateRatingSummary(rating, 3, Now);

        Assert.Equal(rating, caregiver.AverageRating);
    }

    [Fact]
    public void A_negative_review_count_is_rejected()
    {
        var caregiver = CreateCaregiver();

        Assert.Throws<DomainException>(() => caregiver.UpdateRatingSummary(4m, -1, Now));
    }

    [Fact]
    public void A_rating_summary_can_be_recorded()
    {
        var caregiver = CreateCaregiver();

        caregiver.UpdateRatingSummary(4.5m, 12, Now);

        Assert.Equal(4.5m, caregiver.AverageRating);
        Assert.Equal(12, caregiver.ReviewCount);
    }
}
