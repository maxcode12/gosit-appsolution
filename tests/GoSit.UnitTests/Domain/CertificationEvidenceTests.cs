using GoSit.Domain.Caregivers;
using GoSit.Domain.Common;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// The rule that a Verified badge has to be earned by someone looking at something.
///
/// Until certification documents could be uploaded, staff approved from a code and two
/// dates the caregiver had typed themselves — which meant the badge on a profile, and the
/// eligibility to work with children and vulnerable adults behind it, rested on
/// self-declaration.
/// </summary>
public class CertificationEvidenceTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    private static CaregiverProfile CreateProfile() =>
        CaregiverProfile.Create(Guid.NewGuid(), 25m, 3, Now);

    private static CaregiverCertification Submit(CaregiverProfile profile) =>
        profile.SubmitCertification(
            Certification.Create("VULNERABLE_SECTOR", "Vulnerable Sector Check", true),
            new DateOnly(2026, 1, 10),
            new DateOnly(2028, 1, 10),
            Now);

    [Fact]
    public void A_certification_without_a_document_cannot_be_approved()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        Assert.False(submitted.HasDocument);
        Assert.Throws<DomainException>(() => profile.ApproveCertification(submitted.Id, Now));
    }

    [Fact]
    public void A_certification_with_a_document_can_be_approved()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        profile.AttachCertificationDocument(submitted.Id, "2026/08/abc", Now);
        profile.ApproveCertification(submitted.Id, Now);

        Assert.Equal(CertificationStatus.Verified, submitted.Status);
    }

    /// <summary>
    /// Rejection is deliberately still possible without a document — "you did not upload
    /// anything" is one of the most common reasons to refuse one, and a reviewer who
    /// could not act on it would have no way to clear the queue.
    /// </summary>
    [Fact]
    public void A_certification_without_a_document_can_still_be_rejected()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        profile.RejectCertification(submitted.Id, "No certificate was uploaded.", Now);

        Assert.Equal(CertificationStatus.Rejected, submitted.Status);
    }

    /// <summary>
    /// The hole this closes. Without reopening, a caregiver could be approved on one
    /// document and then swap in another, leaving the badge attesting to a file nobody
    /// read — and swapping is the ordinary, innocent action of replacing a blurry photo,
    /// so it is not something to detect after the fact.
    /// </summary>
    [Fact]
    public void Replacing_the_document_after_approval_reopens_the_review()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        profile.AttachCertificationDocument(submitted.Id, "2026/08/first", Now);
        profile.ApproveCertification(submitted.Id, Now);

        profile.AttachCertificationDocument(submitted.Id, "2026/08/second", Now);

        Assert.Equal(CertificationStatus.Pending, submitted.Status);
        Assert.Null(submitted.ReviewedAtUtc);

        // And the eligibility it conferred is gone with it.
        Assert.False(submitted.IsValidOn(DateOnly.FromDateTime(Now)));
    }

    [Fact]
    public void An_empty_document_reference_is_refused()
    {
        var profile = CreateProfile();
        var submitted = Submit(profile);

        Assert.Throws<DomainException>(() =>
            profile.AttachCertificationDocument(submitted.Id, "  ", Now));
    }

    /// <summary>
    /// A submitted certification carries no evidence until one is uploaded. The caregiver
    /// has no way to supply a storage key, which is what stops one caregiver pointing at
    /// another's record check.
    /// </summary>
    [Fact]
    public void A_newly_submitted_certification_has_no_document()
    {
        var profile = CreateProfile();

        Assert.False(Submit(profile).HasDocument);
    }
}
