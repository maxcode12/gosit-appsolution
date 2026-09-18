using GoSit.Domain.Payouts;
using Xunit;

namespace GoSit.UnitTests.Payouts;

/// <summary>
/// When the payout sweep should try again.
///
/// A transfer usually fails for a reason that will not fix itself in fifteen minutes —
/// an account under review, a missing capability — so retrying every cycle would be
/// pointless load against the provider and a noisy audit trail. But it must never stop
/// retrying: the caregiver has earned the money.
/// </summary>
public class PayoutRetryTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Payout Queued() =>
        Payout.Queue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FeeSplit(100m, 15m, 85m),
            0.15m,
            Now,
            Now);

    [Fact]
    public void A_queued_payout_is_sent_immediately()
    {
        Assert.True(Queued().IsReadyToSend(Now));
    }

    [Fact]
    public void An_in_transit_payout_is_not_the_sweeps_business()
    {
        var payout = Queued();
        payout.RecordAttempt(Now);
        payout.MarkInTransit("tr_1");

        Assert.False(payout.IsReadyToSend(Now.AddDays(30)));
    }

    [Fact]
    public void A_paid_payout_is_never_sent_again()
    {
        var payout = Queued();
        payout.RecordAttempt(Now);
        payout.MarkInTransit("tr_1");
        payout.MarkPaid(Now);

        Assert.False(payout.IsReadyToSend(Now.AddDays(30)));
    }

    /// <summary>
    /// The first retry waits fifteen minutes — long enough not to hammer a provider
    /// that is having a moment, short enough that a transient failure clears quickly.
    /// </summary>
    [Fact]
    public void A_failed_payout_waits_before_the_first_retry()
    {
        var payout = Queued();
        payout.RecordAttempt(Now);
        payout.MarkFailed("account_under_review");

        Assert.False(payout.IsReadyToSend(Now.AddMinutes(14)));
        Assert.True(payout.IsReadyToSend(Now.AddMinutes(15)));
    }

    /// <summary>Each failure waits longer than the last.</summary>
    [Fact]
    public void The_wait_grows_with_each_failure()
    {
        var payout = Queued();

        // Second failure: an hour.
        payout.RecordAttempt(Now);
        payout.MarkFailed("still_under_review");
        payout.RecordAttempt(Now.AddMinutes(15));
        payout.MarkFailed("still_under_review");

        Assert.False(payout.IsReadyToSend(Now.AddMinutes(15).AddMinutes(59)));
        Assert.True(payout.IsReadyToSend(Now.AddMinutes(15).AddHours(1)));
    }

    /// <summary>
    /// The backoff is capped, so a payout blocked for a fortnight is still retried daily
    /// rather than drifting to a wait measured in years.
    /// </summary>
    [Fact]
    public void The_wait_is_capped_so_retrying_never_stops()
    {
        var payout = Queued();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            payout.RecordAttempt(Now);
            payout.MarkFailed("still_under_review");
        }

        Assert.False(payout.IsReadyToSend(Now.AddHours(23)));
        Assert.True(payout.IsReadyToSend(Now.AddHours(24)));
    }

    /// <summary>
    /// A failed payout is still owed. Nothing about failing reduces the amount or
    /// removes it — the caregiver did the work.
    /// </summary>
    [Fact]
    public void A_failed_payout_keeps_its_amount_and_stays_owed()
    {
        var payout = Queued();
        payout.RecordAttempt(Now);
        payout.MarkFailed("card_declined");

        Assert.Equal(PayoutStatus.Failed, payout.Status);
        Assert.Equal(85m, payout.Amount);
    }

    /// <summary>
    /// The attempt is recorded before the provider is called, so a transfer that fails
    /// by exception backs off exactly like one that fails by response. Without it, a
    /// provider outage would be retried on every single sweep.
    /// </summary>
    [Fact]
    public void Recording_an_attempt_starts_the_backoff_even_without_a_failure_reason()
    {
        var payout = Queued();
        payout.RecordAttempt(Now);

        Assert.Equal(1, payout.AttemptCount);
        Assert.Equal(Now, payout.LastAttemptAtUtc);
    }

    /// <summary>A retry that succeeds clears the failure rather than leaving it stale.</summary>
    [Fact]
    public void A_successful_retry_clears_the_failure()
    {
        var payout = Queued();
        payout.RecordAttempt(Now);
        payout.MarkFailed("temporary");

        payout.RecordAttempt(Now.AddHours(1));
        payout.MarkInTransit("tr_retry");

        Assert.Equal(PayoutStatus.InTransit, payout.Status);
        Assert.Null(payout.FailureReason);
    }
}
