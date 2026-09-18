using GoSit.Domain.Common;
using GoSit.Domain.TrustAndSafety;
using GoSit.Domain.TrustAndSafety.Events;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class VerificationCaseTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);
    private static readonly Guid CaregiverId = Guid.NewGuid();
    private static readonly Guid StaffId = Guid.NewGuid();

    private static VerificationCase Submit(
        VerificationCheckType type = VerificationCheckType.Identity) =>
        VerificationCase.Submit(CaregiverId, type, "blob/evidence-key", Now);

    [Fact]
    public void A_submitted_case_starts_undecided_and_confers_nothing()
    {
        var @case = Submit();

        Assert.Equal(VerificationCaseStatus.Submitted, @case.Status);
        Assert.False(@case.IsDecided);
        Assert.False(@case.IsValidOn(Today));
        Assert.Null(@case.ReviewedByUserId);
    }

    [Fact]
    public void Staff_approval_makes_the_case_valid()
    {
        var @case = Submit();
        @case.BeginReview("provider-ref-1", Now);

        @case.Approve(StaffId, validUntil: null, notes: "ID matched.", Now);

        Assert.Equal(VerificationCaseStatus.Approved, @case.Status);
        Assert.Equal(StaffId, @case.ReviewedByUserId);
        Assert.True(@case.IsValidOn(Today));
    }

    /// <summary>An approval must name who made it, or there is no accountability.</summary>
    [Fact]
    public void A_decision_without_a_reviewer_is_rejected()
    {
        var @case = Submit();

        Assert.Throws<DomainException>(() => @case.Approve(Guid.Empty, null, null, Now));
        Assert.Throws<DomainException>(() => @case.Reject(Guid.Empty, "No.", Now));
    }

    [Fact]
    public void A_rejection_requires_a_reason()
    {
        var @case = Submit();

        Assert.Throws<DomainException>(() => @case.Reject(StaffId, "  ", Now));
    }

    [Fact]
    public void A_rejected_case_confers_nothing()
    {
        var @case = Submit();

        @case.Reject(StaffId, "The document was unreadable.", Now);

        Assert.False(@case.IsValidOn(Today));
        Assert.Equal("The document was unreadable.", @case.DecisionNotes);
    }

    [Fact]
    public void A_case_cannot_be_decided_twice()
    {
        var @case = Submit();
        @case.Approve(StaffId, null, null, Now);

        Assert.Throws<DomainException>(() => @case.Reject(StaffId, "Changed my mind.", Now));
        Assert.Throws<DomainException>(() => @case.Approve(StaffId, null, null, Now));
    }

    [Fact]
    public void An_approval_stops_counting_after_it_expires()
    {
        var @case = Submit();
        @case.Approve(StaffId, new DateOnly(2027, 1, 1), null, Now);

        Assert.True(@case.IsValidOn(Today));
        Assert.True(@case.IsValidOn(new DateOnly(2027, 1, 1)));
        Assert.False(@case.IsValidOn(new DateOnly(2027, 1, 2)));
    }

    [Fact]
    public void An_approval_cannot_expire_in_the_past()
    {
        var @case = Submit();

        Assert.Throws<DomainException>(() =>
            @case.Approve(StaffId, Today.AddDays(-1), null, Now));
    }

    [Fact]
    public void Only_a_submitted_case_can_move_into_review()
    {
        var @case = Submit();
        @case.Approve(StaffId, null, null, Now);

        Assert.Throws<DomainException>(() => @case.BeginReview(null, Now));
    }

    // ---- listing requirements: care for people ----

    /// <summary>
    /// Identity alone is not enough to work with children or vulnerable adults.
    /// </summary>
    [Fact]
    public void Identity_alone_does_not_satisfy_the_listing_requirements()
    {
        var identity = Submit(VerificationCheckType.Identity);
        identity.Approve(StaffId, null, null, Now);

        Assert.False(VerificationRequirements.AreSatisfiedBy([identity], Today, offersOnlyPetCare: false));
    }

    [Fact]
    public void Identity_plus_a_vulnerable_sector_check_satisfies_them()
    {
        var identity = Submit(VerificationCheckType.Identity);
        identity.Approve(StaffId, null, null, Now);

        var vulnerableSector = Submit(VerificationCheckType.VulnerableSector);
        vulnerableSector.Approve(StaffId, null, null, Now);

        Assert.True(VerificationRequirements.AreSatisfiedBy(
            [identity, vulnerableSector], Today, offersOnlyPetCare: false));
    }

    [Fact]
    public void An_expired_check_stops_satisfying_the_requirements()
    {
        var identity = Submit(VerificationCheckType.Identity);
        identity.Approve(StaffId, null, null, Now);

        var vulnerableSector = Submit(VerificationCheckType.VulnerableSector);
        vulnerableSector.Approve(StaffId, new DateOnly(2026, 12, 31), null, Now);

        Assert.True(VerificationRequirements.AreSatisfiedBy(
            [identity, vulnerableSector], new DateOnly(2026, 12, 31), offersOnlyPetCare: false));
        Assert.False(VerificationRequirements.AreSatisfiedBy(
            [identity, vulnerableSector], new DateOnly(2027, 1, 1), offersOnlyPetCare: false));
    }

    [Fact]
    public void Pending_checks_do_not_satisfy_the_requirements()
    {
        var identity = Submit(VerificationCheckType.Identity);
        var vulnerableSector = Submit(VerificationCheckType.VulnerableSector);

        Assert.False(VerificationRequirements.AreSatisfiedBy(
            [identity, vulnerableSector], Today, offersOnlyPetCare: false));
    }

    // ---- listing requirements: pet care ----

    /// <summary>
    /// Photo ID and a criminal record check clear someone for pet care — and only pet care.
    /// Someone offering Babysitting is held to the people bar however their checks came out.
    /// </summary>
    [Fact]
    public void Identity_plus_a_record_check_clears_for_pets_but_not_people()
    {
        var identity = Submit(VerificationCheckType.Identity);
        identity.Approve(StaffId, null, null, Now);

        var recordCheck = Submit(VerificationCheckType.BackgroundCheck);
        recordCheck.Approve(StaffId, null, null, Now);

        Assert.Equal(VerificationClearance.PetCare,
            VerificationRequirements.ClearanceFrom([identity, recordCheck], Today));

        Assert.True(VerificationRequirements.AreSatisfiedBy(
            [identity, recordCheck], Today, offersOnlyPetCare: true));
        Assert.False(VerificationRequirements.AreSatisfiedBy(
            [identity, recordCheck], Today, offersOnlyPetCare: false));
    }

    /// <summary>A vulnerable sector check includes a record check, so it clears pet care too.</summary>
    [Fact]
    public void A_vulnerable_sector_check_also_clears_pet_care()
    {
        var identity = Submit(VerificationCheckType.Identity);
        identity.Approve(StaffId, null, null, Now);

        var vulnerableSector = Submit(VerificationCheckType.VulnerableSector);
        vulnerableSector.Approve(StaffId, null, null, Now);

        Assert.Equal(VerificationClearance.PeopleCare,
            VerificationRequirements.ClearanceFrom([identity, vulnerableSector], Today));

        Assert.True(VerificationRequirements.AreSatisfiedBy(
            [identity, vulnerableSector], Today, offersOnlyPetCare: true));
    }

    /// <summary>No photo ID, no clearance of any kind, whatever else was approved.</summary>
    [Fact]
    public void Nothing_clears_without_photo_id()
    {
        var recordCheck = Submit(VerificationCheckType.BackgroundCheck);
        recordCheck.Approve(StaffId, null, null, Now);

        var vulnerableSector = Submit(VerificationCheckType.VulnerableSector);
        vulnerableSector.Approve(StaffId, null, null, Now);

        Assert.Equal(VerificationClearance.None,
            VerificationRequirements.ClearanceFrom([recordCheck, vulnerableSector], Today));
    }

    [Fact]
    public void The_checks_asked_for_follow_what_the_caregiver_offers()
    {
        Assert.Equal(
            [VerificationCheckType.Identity, VerificationCheckType.BackgroundCheck],
            VerificationRequirements.RequiredFor(offersOnlyPetCare: true));

        Assert.Equal(
            [VerificationCheckType.Identity, VerificationCheckType.VulnerableSector],
            VerificationRequirements.RequiredFor(offersOnlyPetCare: false));
    }
}

public class IncidentReportTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Reporter = Guid.NewGuid();
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly Guid Staff = Guid.NewGuid();

    private static IncidentReport Raise(
        IncidentCategory category = IncidentCategory.NoShow,
        IncidentSeverity severity = IncidentSeverity.Low) =>
        IncidentReport.Raise(
            Reporter, Subject, Guid.NewGuid(), category, severity, "What happened.", Now);

    [Fact]
    public void A_new_report_is_open()
    {
        var report = Raise();

        Assert.Equal(IncidentStatus.Open, report.Status);
        Assert.Null(report.AssignedToUserId);
        Assert.Null(report.ResolvedAtUtc);
    }

    [Fact]
    public void Raising_a_report_emits_an_event_without_the_description()
    {
        var report = IncidentReport.Raise(
            Reporter, Subject, null, IncidentCategory.SafetyConcern, IncidentSeverity.High,
            "Sam was left alone for an hour.", Now);

        var raised = Assert.IsType<IncidentReportedDomainEvent>(Assert.Single(report.DomainEvents));
        Assert.Equal(IncidentCategory.SafetyConcern, raised.Category);
        // The event carries the classification, never the account of what happened.
        Assert.DoesNotContain("Sam", raised.ToString());
    }

    [Fact]
    public void A_report_cannot_be_raised_about_yourself()
    {
        Assert.Throws<DomainException>(() => IncidentReport.Raise(
            Reporter, Reporter, null, IncidentCategory.Other, IncidentSeverity.Low, "x", Now));
    }

    [Fact]
    public void A_report_needs_a_description()
    {
        Assert.Throws<DomainException>(() => IncidentReport.Raise(
            Reporter, Subject, null, IncidentCategory.Other, IncidentSeverity.Low, "   ", Now));
    }

    /// <summary>
    /// Anything about a care recipient's safety jumps the queue, whatever severity the
    /// reporter chose — people under stress do not always classify accurately.
    /// </summary>
    [Fact]
    public void A_safety_concern_is_urgent_even_at_low_severity()
    {
        var report = Raise(IncidentCategory.SafetyConcern, IncidentSeverity.Low);

        Assert.True(report.RequiresUrgentReview);
    }

    [Fact]
    public void A_critical_report_of_any_category_is_urgent()
    {
        Assert.True(Raise(IncidentCategory.Fraud, IncidentSeverity.Critical).RequiresUrgentReview);
    }

    [Fact]
    public void An_ordinary_report_is_not_urgent()
    {
        Assert.False(Raise(IncidentCategory.NoShow, IncidentSeverity.Low).RequiresUrgentReview);
    }

    [Fact]
    public void A_closed_report_is_no_longer_urgent()
    {
        var report = Raise(IncidentCategory.SafetyConcern, IncidentSeverity.Critical);

        report.Resolve(Staff, "Caregiver suspended pending investigation.", Now);

        Assert.False(report.RequiresUrgentReview);
    }

    [Fact]
    public void Assigning_moves_a_report_under_review()
    {
        var report = Raise();

        report.Assign(Staff);

        Assert.Equal(IncidentStatus.UnderReview, report.Status);
        Assert.Equal(Staff, report.AssignedToUserId);
    }

    [Fact]
    public void Resolving_records_who_and_why()
    {
        var report = Raise();

        report.Resolve(Staff, "Contacted both parties; no further action.", Now);

        Assert.Equal(IncidentStatus.Resolved, report.Status);
        Assert.Equal(Staff, report.AssignedToUserId);
        Assert.Equal("Contacted both parties; no further action.", report.ResolutionNotes);
        Assert.Equal(Now, report.ResolvedAtUtc);
    }

    [Fact]
    public void Dismissing_also_records_who_and_why()
    {
        var report = Raise();

        report.Dismiss(Staff, "Duplicate of an earlier report.", Now);

        Assert.Equal(IncidentStatus.Dismissed, report.Status);
        Assert.NotNull(report.ResolutionNotes);
    }

    /// <summary>An outcome with no reasoning is not an audit trail.</summary>
    [Fact]
    public void Closing_a_report_requires_an_explanation()
    {
        var report = Raise();

        Assert.Throws<DomainException>(() => report.Resolve(Staff, "   ", Now));
        Assert.Throws<DomainException>(() => report.Dismiss(Staff, "", Now));
    }

    [Fact]
    public void Closing_requires_a_staff_member()
    {
        var report = Raise();

        Assert.Throws<DomainException>(() => report.Resolve(Guid.Empty, "Done.", Now));
    }

    [Fact]
    public void A_closed_report_cannot_be_closed_again_or_reassigned()
    {
        var report = Raise();
        report.Resolve(Staff, "Handled.", Now);

        Assert.Throws<DomainException>(() => report.Resolve(Staff, "Again.", Now));
        Assert.Throws<DomainException>(() => report.Assign(Staff));
    }
}
