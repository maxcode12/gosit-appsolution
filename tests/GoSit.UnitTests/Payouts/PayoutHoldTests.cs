using GoSit.Domain.Common;
using GoSit.Domain.Payouts;
using Xunit;

namespace GoSit.UnitTests.Payouts;

/// <summary>
/// Payouts waiting out the dispute window.
///
/// Before this, a payout went to the caregiver within fifteen minutes of the payment
/// settling, while the family had fourteen days to raise a dispute. By the time anyone
/// disputed, the money had already left — so the dispute window was decorative, and Go
/// Sit would have been refunding out of its own pocket or refusing outright.
///
/// The trade-off is real and runs the other way too: a caregiver now waits two weeks to
/// be paid for a shift they have already worked. That is a cash-flow decision rather than
/// an engineering one, and it is recorded as such in PLAN.md — what these tests pin is
/// that the money cannot move while it is still disputable.
/// </summary>
public class PayoutHoldTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime AvailableAt = Now.AddDays(14);

    private static Payout Queued(DateTime? availableAt = null) =>
        Payout.Queue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FeeSplit(100m, 15m, 85m),
            0.15m,
            availableAt ?? AvailableAt,
            Now);

    [Fact]
    public void A_payout_is_not_sent_before_the_dispute_window_closes()
    {
        var payout = Queued();

        Assert.False(payout.IsReadyToSend(Now));
        Assert.False(payout.IsReadyToSend(AvailableAt.AddSeconds(-1)));
    }

    [Fact]
    public void A_payout_is_sent_once_the_window_has_closed()
    {
        var payout = Queued();

        Assert.True(payout.IsReadyToSend(AvailableAt));
        Assert.True(payout.IsReadyToSend(AvailableAt.AddDays(1)));
    }

    /// <summary>
    /// A dispute stops the money outright, rather than merely delaying it. There is no
    /// date at which an unresolved dispute should quietly pay out.
    /// </summary>
    [Fact]
    public void A_disputed_payout_is_never_sent_however_long_it_waits()
    {
        var payout = Queued();
        payout.HoldForDispute();

        Assert.False(payout.IsReadyToSend(AvailableAt));
        Assert.False(payout.IsReadyToSend(AvailableAt.AddYears(1)));
    }

    [Fact]
    public void Releasing_a_dispute_lets_the_payout_go()
    {
        var payout = Queued();
        payout.HoldForDispute();
        payout.ReleaseFromDispute();

        Assert.True(payout.IsReadyToSend(AvailableAt));
    }

    /// <summary>
    /// Releasing does not shortcut the window. A dispute raised and dropped on day one
    /// leaves twelve days still to run, and paying early because someone changed their
    /// mind would reopen the hole for the next person.
    /// </summary>
    [Fact]
    public void Releasing_a_dispute_does_not_shortcut_the_window()
    {
        var payout = Queued();
        payout.HoldForDispute();
        payout.ReleaseFromDispute();

        Assert.False(payout.IsReadyToSend(Now));
    }

    /// <summary>
    /// Money already handed to the provider cannot be held. Pretending otherwise would
    /// make GoSit's books disagree with the provider's, and recovering a sent transfer
    /// is a reversal — a different operation, and a different conversation.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_payout_already_sent_cannot_be_held(bool paid)
    {
        var payout = Queued();

        payout.RecordAttempt(AvailableAt);
        payout.MarkInTransit("tr_test");

        if (paid)
        {
            payout.MarkPaid(AvailableAt);
        }

        Assert.Throws<DomainException>(payout.HoldForDispute);
    }

    /// <summary>
    /// A failed payout is still holdable — it is queued for retry, so the money has not
    /// gone anywhere, and a dispute raised between attempts has to stop the next one.
    /// </summary>
    [Fact]
    public void A_failed_payout_can_still_be_held()
    {
        var payout = Queued();

        payout.RecordAttempt(AvailableAt);
        payout.MarkFailed("insufficient_funds");

        payout.HoldForDispute();

        Assert.False(payout.IsReadyToSend(AvailableAt.AddDays(30)));
    }
}
