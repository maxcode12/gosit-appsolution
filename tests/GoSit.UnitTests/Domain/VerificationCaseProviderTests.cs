using GoSit.Domain.TrustAndSafety;
using Xunit;

namespace GoSit.UnitTests.Domain;

/// <summary>
/// A screening provider's report is evidence on a case, never a decision. These pin the
/// two things that keep it that way.
/// </summary>
public class VerificationCaseProviderTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);

    private static VerificationCase InReview()
    {
        var verificationCase = VerificationCase.Submit(
            Guid.NewGuid(), VerificationCheckType.Identity, null, Now);

        verificationCase.BeginReview("provider-case-1", Now);

        return verificationCase;
    }

    [Fact]
    public void A_provider_report_is_recorded_without_changing_the_status_of_the_case()
    {
        var verificationCase = InReview();

        var recorded = verificationCase.RecordProviderUpdate("COMPLETE", "CLEAR", Now.AddHours(1));

        Assert.True(recorded);
        Assert.Equal("COMPLETE", verificationCase.ProviderStatus);
        Assert.Equal("CLEAR", verificationCase.ProviderScore);
        Assert.Equal(Now.AddHours(1), verificationCase.ProviderUpdatedAtUtc);

        // A clear result approves nobody. Only a staff decision moves the case.
        Assert.Equal(VerificationCaseStatus.InReview, verificationCase.Status);
        Assert.False(verificationCase.IsValidOn(DateOnly.FromDateTime(Now)));
    }

    [Fact]
    public void A_later_report_replaces_an_earlier_one()
    {
        var verificationCase = InReview();

        verificationCase.RecordProviderUpdate("IN_PROGRESS", null, Now.AddHours(1));
        verificationCase.RecordProviderUpdate("COMPLETE", "REVIEW", Now.AddHours(2));

        Assert.Equal("COMPLETE", verificationCase.ProviderStatus);
        Assert.Equal("REVIEW", verificationCase.ProviderScore);
    }

    [Fact]
    public void Once_staff_have_decided_the_provider_cannot_change_what_they_decided_on()
    {
        var verificationCase = InReview();
        verificationCase.RecordProviderUpdate("COMPLETE", "CLEAR", Now.AddHours(1));
        verificationCase.Approve(Guid.NewGuid(), null, null, Now.AddHours(2));

        var recorded = verificationCase.RecordProviderUpdate("COMPLETE", "REJECT", Now.AddHours(3));

        Assert.False(recorded);
        Assert.Equal("CLEAR", verificationCase.ProviderScore);
    }

    [Fact]
    public void Overlong_values_are_cut_rather_than_refused()
    {
        var verificationCase = InReview();

        verificationCase.RecordProviderUpdate(new string('s', 500), new string('x', 500), Now);

        Assert.Equal(64, verificationCase.ProviderStatus!.Length);
        Assert.Equal(32, verificationCase.ProviderScore!.Length);
    }
}
