using GoSit.Domain.CareRecipients;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// Warning a caregiver before a certification costs them work.
///
/// Expiry has always been enforced — a lapsed certificate drops the caregiver out of
/// search for every service requiring it and stops them accepting a booking. Nothing ever
/// said so. Their work quietly stopped arriving and the platform never connected the two
/// for them.
///
/// These pin the two things that make the warning worth sending: it is said once, and it
/// says what the lapse costs.
/// </summary>
public class CertificationExpiryNoticeTests
{
    private const int DaysAhead = 30;

    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static CaregiverProfile CreateProfile() =>
        CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);

    /// <summary>A verified certification expiring on the given date.</summary>
    private static CaregiverCertification Hold(
        CaregiverProfile profile, DateOnly expiresOn, string code = "FIRST_AID")
    {
        var submitted = profile.SubmitCertification(
            Certification.Create(code, "First Aid", true),
            new DateOnly(2026, 1, 10),
            expiresOn,
            Now);

        profile.AttachCertificationDocument(submitted.Id, "2026/08/abc", Now);
        profile.ApproveCertification(submitted.Id, Now);

        return submitted;
    }

    [Fact]
    public void A_certification_comfortably_in_date_says_nothing()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(DaysAhead + 1));

        Assert.Empty(profile.TakeExpiryNotices(Today, DaysAhead));
    }

    [Fact]
    public void A_certification_inside_the_window_is_reported_as_expiring()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(10));

        var notice = Assert.Single(profile.TakeExpiryNotices(Today, DaysAhead));

        Assert.Equal(ExpiryNoticeStage.Expiring, notice.Stage);
    }

    /// <summary>
    /// The whole reason the stage is stored. The sweeper runs four times a day, and a
    /// caregiver warned every six hours for a month stops reading any of it.
    /// </summary>
    [Fact]
    public void A_caregiver_is_warned_once_however_often_the_sweep_runs()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(10));

        Assert.Single(profile.TakeExpiryNotices(Today, DaysAhead));
        Assert.Empty(profile.TakeExpiryNotices(Today, DaysAhead));
        Assert.Empty(profile.TakeExpiryNotices(Today.AddDays(1), DaysAhead));
    }

    /// <summary>
    /// The second notice is not a repeat. It is the one that explains why the work
    /// stopped, which is the question the caregiver actually has.
    /// </summary>
    [Fact]
    public void A_caregiver_already_warned_is_told_again_when_it_actually_lapses()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(10));

        Assert.Single(profile.TakeExpiryNotices(Today, DaysAhead));

        var lapsed = Assert.Single(profile.TakeExpiryNotices(Today.AddDays(11), DaysAhead));

        Assert.Equal(ExpiryNoticeStage.Expired, lapsed.Stage);

        // And that is the last of it.
        Assert.Empty(profile.TakeExpiryNotices(Today.AddDays(12), DaysAhead));
    }

    /// <summary>
    /// Someone whose certificate lapsed before anyone thought to warn them gets the
    /// notice that matters, not the one that no longer applies.
    /// </summary>
    [Fact]
    public void A_certification_that_lapsed_unnoticed_skips_straight_to_expired()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(5));

        // No sweep ran while it was still in date — the case every existing certificate
        // was in on the day this feature shipped.
        var notice = Assert.Single(profile.TakeExpiryNotices(Today.AddDays(6), DaysAhead));

        Assert.Equal(ExpiryNoticeStage.Expired, notice.Stage);
    }

    /// <summary>
    /// A pending certification is earning them nothing, so its lapsing costs them
    /// nothing. Warning about it would be noise stacked on a review they are waiting for.
    /// </summary>
    [Fact]
    public void An_unverified_certification_is_not_warned_about()
    {
        var profile = CreateProfile();

        profile.SubmitCertification(
            Certification.Create("CPR", "CPR", true),
            new DateOnly(2026, 1, 10),
            Today.AddDays(5),
            Now);

        Assert.Empty(profile.TakeExpiryNotices(Today, DaysAhead));
    }

    /// <summary>
    /// The half of the notice that makes it worth reading. A caregiver deciding whether
    /// to spend a Saturday on a refresher course is deciding about the lost services.
    /// </summary>
    [Fact]
    public void The_notice_names_the_services_the_lapse_would_cost()
    {
        var profile = CreateProfile();

        var afterSchool = CareService.Create(
            Guid.NewGuid(),
            "AFTER_SCHOOL_CARE",
            "After-School Care",
            null,
            ["FIRST_AID"],
            [CareRecipientType.Child]);

        var companionship = CareService.Create(
            Guid.NewGuid(),
            "SENIOR_COMPANIONSHIP",
            "Senior Companionship",
            null,
            [],
            [CareRecipientType.Senior]);

        profile.ReplaceServices([afterSchool, companionship], Now);
        Hold(profile, Today.AddDays(10));

        var notice = Assert.Single(profile.TakeExpiryNotices(Today, DaysAhead));

        // Only the service that actually requires it.
        Assert.Equal(["After-School Care"], notice.AffectedServiceNames);
    }

    /// <summary>
    /// A certificate no offered service requires is still worth a warning — they may be
    /// keeping it to add a service later — but it has cost them nothing, and the notice
    /// must not claim otherwise.
    /// </summary>
    [Fact]
    public void A_certification_no_offered_service_requires_costs_nothing()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(10));

        var notice = Assert.Single(profile.TakeExpiryNotices(Today, DaysAhead));

        Assert.Empty(notice.AffectedServiceNames);
    }
}
