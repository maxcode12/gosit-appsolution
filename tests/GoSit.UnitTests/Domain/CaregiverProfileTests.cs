using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using GoSit.Domain.Common.ValueObjects;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class CaregiverProfileTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static CaregiverProfile CreateProfile() =>
        CaregiverProfile.Create(Guid.NewGuid(), hourlyRate: 25m, yearsOfExperience: 3, Now);

    private static Certification Cpr() => Certification.Create("CPR", "CPR Certification", requiresExpiry: true);

    private static Certification Ece() =>
        Certification.Create("ECE", "Early Childhood Education", requiresExpiry: false);

    [Fact]
    public void A_new_caregiver_is_unverified_and_not_accepting_work()
    {
        var profile = CreateProfile();

        Assert.Equal(VerificationStatus.Unverified, profile.VerificationStatus);
        Assert.False(profile.AcceptingWork);
        Assert.False(profile.IsListable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17.59)]
    [InlineData(250)]
    public void An_out_of_range_hourly_rate_is_rejected(decimal hourlyRate)
    {
        Assert.Throws<DomainException>(() =>
            CaregiverProfile.Create(Guid.NewGuid(), hourlyRate, 3, Now));
    }

    /// <summary>The floor itself is allowed — Ontario's general minimum wage.</summary>
    [Fact]
    public void An_hourly_rate_at_the_minimum_is_accepted()
    {
        var profile = CaregiverProfile.Create(Guid.NewGuid(), 17.60m, 3, Now);

        Assert.Equal(17.60m, profile.HourlyRate);
    }

    [Fact]
    public void An_hourly_rate_is_rounded_to_cents()
    {
        var profile = CreateProfile();

        profile.SetRate(25.126m, Now);

        Assert.Equal(25.13m, profile.HourlyRate);
    }

    /// <summary>
    /// An unverified caregiver must never be listed, however complete their profile.
    /// </summary>
    [Fact]
    public void An_unverified_caregiver_cannot_accept_work()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.SetAcceptingWork(true, Now));
    }

    [Fact]
    public void A_verified_caregiver_becomes_listable_once_the_profile_is_complete()
    {
        var profile = CreateProfile();
        profile.MarkVerified(clearedForPeopleCare: true, Now);
        profile.SetAcceptingWork(true, Now);

        // Still missing an address, availability, and a signed service agreement.
        Assert.False(profile.IsListable);

        profile.SetServiceArea(
            Address.Create("12 Bloor St W", null, "Toronto", "ON", "M5S 1M2"), 15, Now);
        profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))], Now);

        // Still unsigned.
        Assert.False(profile.IsListable);

        profile.AcceptServiceAgreement("Ada Lovelace", Now);

        Assert.True(profile.IsListable);
    }

    /// <summary>
    /// A caregiver cannot be listed on a stale signature. Bumping the agreement's
    /// version — a real revision, once the draft is lawyer-approved — must make every
    /// existing signature insufficient until it is redone.
    /// </summary>
    [Fact]
    public void A_signature_only_counts_for_the_agreement_version_it_was_given_for()
    {
        var profile = CreateProfile();
        profile.AcceptServiceAgreement("Ada Lovelace", Now);

        Assert.Equal(ServiceAgreement.CurrentVersion, profile.AgreementAcceptedVersion);
        Assert.True(profile.HasAcceptedCurrentAgreement);
    }

    [Fact]
    public void Signing_requires_a_typed_name()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.AcceptServiceAgreement("   ", Now));
    }

    [Fact]
    public void Rejecting_verification_also_delists_the_caregiver()
    {
        var profile = CreateProfile();
        profile.MarkVerified(clearedForPeopleCare: true, Now);
        profile.SetAcceptingWork(true, Now);

        profile.MarkVerificationRejected(Now);

        Assert.False(profile.AcceptingWork);
        Assert.False(profile.IsListable);
    }

    [Fact]
    public void Overlapping_availability_on_the_same_day_is_rejected()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.ReplaceAvailability(
        [
            new AvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            new AvailabilityWindow(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(17, 0)),
        ], Now));
    }

    [Fact]
    public void The_same_hours_on_different_days_do_not_overlap()
    {
        var profile = CreateProfile();

        profile.ReplaceAvailability(
        [
            new AvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            new AvailabilityWindow(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
        ], Now);

        Assert.Equal(2, profile.Availability.Count);
    }

    /// <summary>
    /// This used to assert the opposite — that an end before the start was rejected —
    /// which is what made "Overnight Child Care" unbookable while GoSit was selling it.
    /// An end earlier in the day now means the shift runs past midnight.
    /// </summary>
    [Fact]
    public void An_availability_window_may_run_past_midnight()
    {
        var profile = CreateProfile();

        profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(20, 0), new TimeOnly(7, 0))], Now);

        var slot = Assert.Single(profile.Availability);

        Assert.True(slot.CrossesMidnight);
        // Eleven hours, measured around the clock rather than backwards through it.
        Assert.Equal(11 * 60, slot.Interval.LengthMinutes);
    }

    /// <summary>
    /// Equal times are the one case still refused. They could mean a full day or none at
    /// all, and guessing wrong in either direction is worse than asking.
    /// </summary>
    [Fact]
    public void An_availability_window_with_equal_times_is_refused()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 0))], Now));
    }

    /// <summary>
    /// A window running past midnight collides with the next morning's. The old
    /// same-day overlap check could not see that, so it would have accepted an hour of
    /// double-booking.
    /// </summary>
    [Fact]
    public void An_overnight_window_cannot_overlap_the_next_mornings()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.ReplaceAvailability(
            [
                new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(20, 0), new TimeOnly(7, 0)),
                new AvailabilityWindow(DayOfWeek.Saturday, new TimeOnly(6, 0), new TimeOnly(10, 0)),
            ],
            Now));
    }

    [Fact]
    public void Replacing_availability_discards_the_previous_schedule()
    {
        var profile = CreateProfile();
        profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0))], Now);

        profile.ReplaceAvailability(
            [new AvailabilityWindow(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(13, 0))], Now);

        Assert.Equal(DayOfWeek.Friday, profile.Availability.Single().Day);
    }

    /// <summary>
    /// A caregiver states what they hold; only Trust and Safety decides it counts.
    /// </summary>
    [Fact]
    public void A_submitted_certification_starts_pending_and_confers_nothing()
    {
        var profile = CreateProfile();

        var submitted = profile.SubmitCertification(
            Cpr(), new DateOnly(2026, 1, 10), new DateOnly(2028, 1, 10), Now);

        Assert.Equal(CertificationStatus.Pending, submitted.Status);
        Assert.False(submitted.IsValidOn(Today));
        Assert.Empty(profile.ValidCertificationCodesOn(Today));
        Assert.False(profile.MeetsRequirements(["CPR"], Today));
    }

    [Fact]
    public void An_approved_certification_counts_towards_eligibility()
    {
        var profile = CreateProfile();
        var submitted = profile.SubmitCertification(
            Cpr(), new DateOnly(2026, 1, 10), new DateOnly(2028, 1, 10), Now);

        // Approval now requires evidence — see CaregiverCertification.Approve.
        profile.AttachCertificationDocument(submitted.Id, "test/document", Now);
        profile.ApproveCertification(submitted.Id, Now);

        Assert.True(profile.MeetsRequirements(["CPR"], Today));
        Assert.True(profile.MeetsRequirements(["cpr"], Today), "codes are compared case-insensitively");
    }

    [Fact]
    public void A_rejected_certification_confers_nothing()
    {
        var profile = CreateProfile();
        var submitted = profile.SubmitCertification(
            Cpr(), new DateOnly(2026, 1, 10), new DateOnly(2028, 1, 10), Now);

        profile.RejectCertification(submitted.Id, "The document was unreadable.", Now);

        Assert.False(profile.MeetsRequirements(["CPR"], Today));
    }

    [Fact]
    public void An_expired_certification_stops_counting()
    {
        var profile = CreateProfile();
        var submitted = profile.SubmitCertification(
            Cpr(), new DateOnly(2026, 1, 10), new DateOnly(2026, 12, 31), Now);
        // Approval now requires evidence — see CaregiverCertification.Approve.
        profile.AttachCertificationDocument(submitted.Id, "test/document", Now);
        profile.ApproveCertification(submitted.Id, Now);

        Assert.True(profile.MeetsRequirements(["CPR"], Today));
        Assert.False(profile.MeetsRequirements(["CPR"], new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void A_certification_requiring_expiry_cannot_be_submitted_without_one()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() =>
            profile.SubmitCertification(Cpr(), new DateOnly(2026, 1, 10), null, Now));
    }

    [Fact]
    public void A_certification_not_requiring_expiry_can_omit_one()
    {
        var profile = CreateProfile();

        var submitted = profile.SubmitCertification(Ece(), new DateOnly(2020, 6, 1), null, Now);

        Assert.Null(submitted.ExpiresOn);
    }

    [Fact]
    public void An_already_expired_certification_is_rejected_on_submission()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.SubmitCertification(
            Cpr(), new DateOnly(2020, 1, 10), new DateOnly(2022, 1, 10), Now));
    }

    [Fact]
    public void A_future_issue_date_is_rejected()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.SubmitCertification(
            Cpr(), Today.AddDays(1), Today.AddYears(2), Now));
    }

    [Fact]
    public void The_same_certification_cannot_be_submitted_twice_while_it_is_live()
    {
        var profile = CreateProfile();
        var cpr = Cpr();
        profile.SubmitCertification(cpr, new DateOnly(2026, 1, 10), new DateOnly(2028, 1, 10), Now);

        Assert.Throws<DomainException>(() => profile.SubmitCertification(
            cpr, new DateOnly(2026, 2, 10), new DateOnly(2028, 2, 10), Now));
    }

    [Fact]
    public void MeetsRequirements_needs_every_required_certification()
    {
        var profile = CreateProfile();
        var cpr = profile.SubmitCertification(
            Cpr(), new DateOnly(2026, 1, 10), new DateOnly(2028, 1, 10), Now);
        // Approval now requires evidence — see CaregiverCertification.Approve.
        profile.AttachCertificationDocument(cpr.Id, "test/document", Now);
        profile.ApproveCertification(cpr.Id, Now);

        Assert.True(profile.MeetsRequirements(["CPR"], Today));
        Assert.False(profile.MeetsRequirements(["CPR", "FIRST_AID"], Today));
        Assert.True(profile.MeetsRequirements([], Today), "no requirements means anyone qualifies");
    }

    [Fact]
    public void A_service_radius_outside_the_supported_range_is_rejected()
    {
        var profile = CreateProfile();
        var address = Address.Create("12 Bloor St W", null, "Toronto", "ON", "M5S 1M2");

        Assert.Throws<DomainException>(() => profile.SetServiceArea(address, 0, Now));
        Assert.Throws<DomainException>(() => profile.SetServiceArea(address, 101, Now));
    }
}
