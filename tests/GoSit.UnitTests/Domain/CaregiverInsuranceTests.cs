using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// Caregiver insurance is verified by GoSit, never issued or held by it — see
/// docs/BUSINESS_MODEL.md and docs/ADR-004-marketplace-provider-model.md. This mirrors
/// <see cref="CertificationEvidenceTests"/>: a policy needs an uploaded document before
/// staff can approve it, and swapping the document after approval reopens the review.
/// </summary>
public class CaregiverInsuranceTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    private static CaregiverProfile CreateProfile() =>
        CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);

    private static CaregiverInsurancePolicy Submit(CaregiverProfile profile) =>
        profile.SubmitInsurance(
            InsuranceCoverageType.GeneralLiability,
            "Aviva Canada",
            "POL-12345",
            2_000_000m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1),
            Now);

    [Fact]
    public void A_policy_without_a_document_cannot_be_approved()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        Assert.False(submitted.HasDocument);
        Assert.Throws<DomainException>(() => profile.ApproveInsurance(submitted.Id, Now));
    }

    [Fact]
    public void A_policy_with_a_document_can_be_approved()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        profile.AttachInsuranceDocument(submitted.Id, "2026/08/policy.pdf", Now);
        profile.ApproveInsurance(submitted.Id, Now);

        Assert.Equal(InsuranceVerificationStatus.Verified, submitted.Status);
        Assert.True(profile.HasValidInsurance(InsuranceCoverageType.GeneralLiability, DateOnly.FromDateTime(Now)));
    }

    [Fact]
    public void A_policy_without_a_document_can_still_be_rejected()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        profile.RejectInsurance(submitted.Id, "No proof of insurance was uploaded.", Now);

        Assert.Equal(InsuranceVerificationStatus.Rejected, submitted.Status);
    }

    [Fact]
    public void Replacing_the_document_after_approval_reopens_the_review()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        profile.AttachInsuranceDocument(submitted.Id, "2026/08/first.pdf", Now);
        profile.ApproveInsurance(submitted.Id, Now);

        profile.AttachInsuranceDocument(submitted.Id, "2026/08/second.pdf", Now);

        Assert.Equal(InsuranceVerificationStatus.Pending, submitted.Status);
        Assert.Null(submitted.ReviewedAtUtc);
        Assert.False(submitted.IsValidOn(DateOnly.FromDateTime(Now)));
    }

    [Fact]
    public void A_policy_cannot_be_submitted_already_expired()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.SubmitInsurance(
            InsuranceCoverageType.GeneralLiability,
            "Aviva Canada",
            "POL-1",
            2_000_000m,
            new DateOnly(2024, 1, 1),
            new DateOnly(2025, 1, 1),
            Now));
    }

    [Fact]
    public void An_expiry_before_the_effective_date_is_refused()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.SubmitInsurance(
            InsuranceCoverageType.GeneralLiability,
            "Aviva Canada",
            "POL-1",
            2_000_000m,
            new DateOnly(2027, 1, 1),
            new DateOnly(2026, 1, 1),
            Now));
    }

    [Fact]
    public void A_zero_liability_limit_is_refused()
    {
        var profile = CreateProfile();

        Assert.Throws<DomainException>(() => profile.SubmitInsurance(
            InsuranceCoverageType.GeneralLiability,
            "Aviva Canada",
            "POL-1",
            0m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1),
            Now));
    }

    /// <summary>
    /// Insurance is tracked by coverage type — approval of a general liability policy
    /// says nothing about whether the caregiver holds auto coverage.
    /// </summary>
    [Fact]
    public void Coverage_types_are_not_interchangeable()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        profile.AttachInsuranceDocument(submitted.Id, "2026/08/policy.pdf", Now);
        profile.ApproveInsurance(submitted.Id, Now);

        Assert.False(profile.HasValidInsurance(InsuranceCoverageType.Automobile, DateOnly.FromDateTime(Now)));
    }
}

/// <summary>Expiry warnings for insurance, mirroring <see cref="CertificationExpiryNoticeTests"/>.</summary>
public class CaregiverInsuranceExpiryNoticeTests
{
    private const int DaysAhead = 30;

    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static CaregiverProfile CreateProfile() =>
        CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);

    private static CaregiverInsurancePolicy Hold(CaregiverProfile profile, DateOnly expiresOn)
    {
        var submitted = profile.SubmitInsurance(
            InsuranceCoverageType.GeneralLiability,
            "Aviva Canada",
            "POL-1",
            2_000_000m,
            new DateOnly(2026, 1, 1),
            expiresOn,
            Now);

        profile.AttachInsuranceDocument(submitted.Id, "2026/08/policy.pdf", Now);
        profile.ApproveInsurance(submitted.Id, Now);

        return submitted;
    }

    [Fact]
    public void A_policy_comfortably_in_date_says_nothing()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(DaysAhead + 1));

        Assert.Empty(profile.TakeInsuranceExpiryNotices(Today, DaysAhead));
    }

    [Fact]
    public void A_policy_inside_the_window_is_reported_as_expiring()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(10));

        var notice = Assert.Single(profile.TakeInsuranceExpiryNotices(Today, DaysAhead));

        Assert.Equal(ExpiryNoticeStage.Expiring, notice.Stage);
    }

    [Fact]
    public void A_caregiver_is_warned_once_however_often_the_sweep_runs()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(10));

        Assert.Single(profile.TakeInsuranceExpiryNotices(Today, DaysAhead));
        Assert.Empty(profile.TakeInsuranceExpiryNotices(Today, DaysAhead));
    }

    [Fact]
    public void A_caregiver_already_warned_is_told_again_when_it_actually_lapses()
    {
        var profile = CreateProfile();
        Hold(profile, Today.AddDays(10));

        Assert.Single(profile.TakeInsuranceExpiryNotices(Today, DaysAhead));

        var lapsed = Assert.Single(profile.TakeInsuranceExpiryNotices(Today.AddDays(11), DaysAhead));

        Assert.Equal(ExpiryNoticeStage.Expired, lapsed.Stage);
        Assert.Empty(profile.TakeInsuranceExpiryNotices(Today.AddDays(12), DaysAhead));
    }

    [Fact]
    public void A_pending_policy_is_not_warned_about()
    {
        var profile = CreateProfile();

        profile.SubmitInsurance(
            InsuranceCoverageType.GeneralLiability,
            "Aviva Canada",
            "POL-1",
            2_000_000m,
            new DateOnly(2026, 1, 1),
            Today.AddDays(5),
            Now);

        Assert.Empty(profile.TakeInsuranceExpiryNotices(Today, DaysAhead));
    }
}
