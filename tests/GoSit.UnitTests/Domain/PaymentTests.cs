using GoSit.Domain.Common;
using GoSit.Domain.Payments;
using GoSit.Domain.Payments.Events;
using GoSit.Domain.Payouts;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class MoneyTests
{
    [Theory]
    [InlineData(0, 0L)]
    [InlineData(1, 100L)]
    [InlineData(0.01, 1L)]
    [InlineData(128.00, 12800L)]
    [InlineData(37.50, 3750L)]
    [InlineData(1234.56, 123456L)]
    public void Amounts_convert_to_cents(decimal amount, long expected)
    {
        Assert.Equal(expected, Money.ToMinorUnits(amount));
    }

    [Fact]
    public void Cents_convert_back_to_amounts()
    {
        Assert.Equal(128.00m, Money.FromMinorUnits(12800));
        Assert.Equal(0.01m, Money.FromMinorUnits(1));
    }

    [Fact]
    public void Conversion_round_trips_exactly()
    {
        foreach (var amount in new[] { 0.01m, 17.20m, 37.50m, 128m, 199.99m })
        {
            Assert.Equal(amount, Money.FromMinorUnits(Money.ToMinorUnits(amount)));
        }
    }

    /// <summary>
    /// Silently rounding here would charge someone a different amount than the one
    /// agreed. Fractions of a cent must be caught before they reach the provider.
    /// </summary>
    [Theory]
    [InlineData(0.001)]
    [InlineData(1.005)]
    [InlineData(33.333)]
    public void An_amount_that_is_not_a_whole_number_of_cents_is_rejected(decimal amount)
    {
        Assert.Throws<DomainException>(() => Money.ToMinorUnits(amount));
    }

    [Fact]
    public void Negative_amounts_are_rejected_in_both_directions()
    {
        Assert.Throws<DomainException>(() => Money.ToMinorUnits(-1m));
        Assert.Throws<DomainException>(() => Money.FromMinorUnits(-1));
    }
}

public class PaymentTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid CaregiverId = Guid.NewGuid();

    private static Payment Create(decimal amount = 128m) =>
        Payment.Create(BookingId, RequesterId, CaregiverId, amount, Now);

    private static Payment Settled(decimal amount = 128m)
    {
        var payment = Create(amount);
        payment.MarkSucceeded("pi_test_123", amount, Now);
        payment.ClearDomainEvents();
        return payment;
    }

    [Fact]
    public void A_new_payment_is_pending_with_no_provider_reference()
    {
        var payment = Create();

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.ProviderPaymentIntentId);
        Assert.Equal("CAD", payment.Currency);
        Assert.Equal(0m, payment.RefundedAmount);
    }

    /// <summary>
    /// The same booking must always produce the same key, so a retried request reuses
    /// the provider's original intent rather than creating a second charge.
    /// </summary>
    [Fact]
    public void The_idempotency_key_is_derived_from_the_booking()
    {
        Assert.Equal(Create().IdempotencyKey, Create().IdempotencyKey);
        Assert.Contains(BookingId.ToString("N"), Create().IdempotencyKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_non_positive_amount_is_rejected(decimal amount)
    {
        Assert.Throws<DomainException>(() =>
            Payment.Create(BookingId, RequesterId, CaregiverId, amount, Now));
    }

    [Fact]
    public void An_amount_with_sub_cent_precision_is_rejected_at_creation()
    {
        Assert.Throws<DomainException>(() =>
            Payment.Create(BookingId, RequesterId, CaregiverId, 10.005m, Now));
    }

    [Fact]
    public void Succeeding_records_the_provider_reference_and_raises_an_event()
    {
        var payment = Create();

        payment.MarkSucceeded("pi_test_123", 128m, Now);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("pi_test_123", payment.ProviderPaymentIntentId);
        Assert.Equal(Now, payment.SucceededAtUtc);
        Assert.IsType<PaymentSucceededDomainEvent>(Assert.Single(payment.DomainEvents));
    }

    /// <summary>
    /// Webhooks are delivered at least once, so a repeat must not raise a second event
    /// or double-count anything.
    /// </summary>
    [Fact]
    public void Succeeding_twice_is_idempotent()
    {
        var payment = Create();
        payment.MarkSucceeded("pi_test_123", 128m, Now);
        payment.ClearDomainEvents();

        payment.MarkSucceeded("pi_test_123", 128m, Now.AddMinutes(1));

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Empty(payment.DomainEvents);
    }

    /// <summary>
    /// If the provider says it collected a different amount, the two sides disagree
    /// about money. That must surface, not be quietly accepted.
    /// </summary>
    [Fact]
    public void An_amount_that_does_not_match_is_refused()
    {
        var payment = Create(128m);

        Assert.Throws<DomainException>(() => payment.MarkSucceeded("pi_test_123", 1m, Now));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void A_payment_cannot_be_attached_to_two_different_intents()
    {
        var payment = Create();
        payment.AttachProviderIntent("pi_one");

        Assert.Throws<DomainException>(() => payment.AttachProviderIntent("pi_two"));
    }

    [Fact]
    public void Failing_records_the_code_and_raises_an_event()
    {
        var payment = Create();

        payment.MarkFailed("card_declined", Now);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("card_declined", payment.FailureCode);
        Assert.IsType<PaymentFailedDomainEvent>(Assert.Single(payment.DomainEvents));
    }

    /// <summary>
    /// A late failure event must not un-settle money that was actually taken.
    /// </summary>
    [Fact]
    public void A_failure_arriving_after_success_is_ignored()
    {
        var payment = Settled();

        payment.MarkFailed("card_declined", Now.AddMinutes(5));

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public void A_settled_payment_cannot_be_cancelled()
    {
        var payment = Settled();

        Assert.Throws<DomainException>(() => payment.Cancel(Now));
    }

    [Fact]
    public void An_unsettled_payment_cannot_be_refunded()
    {
        var payment = Create();

        Assert.Throws<DomainException>(() => payment.RecordRefund("re_1", 10m, null, Now));
    }

    [Fact]
    public void A_partial_refund_leaves_the_payment_partially_refunded()
    {
        var payment = Settled(128m);

        payment.RecordRefund("re_1", 28m, "Left early.", Now);

        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(28m, payment.RefundedAmount);
        Assert.Equal(100m, payment.NetAmount);
    }

    [Fact]
    public void Refunding_the_full_amount_marks_it_refunded()
    {
        var payment = Settled(128m);

        payment.RecordRefund("re_1", 128m, null, Now);

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(0m, payment.NetAmount);
    }

    [Fact]
    public void Refunds_can_accumulate_up_to_the_amount_paid()
    {
        var payment = Settled(100m);

        payment.RecordRefund("re_1", 40m, null, Now);
        payment.RecordRefund("re_2", 60m, null, Now);

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(100m, payment.RefundedAmount);
    }

    /// <summary>Refunding more than was collected would be handing out money.</summary>
    [Fact]
    public void Refunds_cannot_exceed_the_amount_paid()
    {
        var payment = Settled(100m);
        payment.RecordRefund("re_1", 80m, null, Now);

        Assert.Throws<DomainException>(() => payment.RecordRefund("re_2", 30m, null, Now));
        Assert.Equal(80m, payment.RefundedAmount);
    }

    [Fact]
    public void The_same_provider_refund_arriving_twice_only_counts_once()
    {
        var payment = Settled(100m);

        payment.RecordRefund("re_1", 40m, null, Now);
        payment.RecordRefund("re_1", 40m, null, Now.AddMinutes(1));

        Assert.Equal(40m, payment.RefundedAmount);
        Assert.Single(payment.Refunds);
    }

    [Fact]
    public void Only_the_two_participants_are_recognised()
    {
        var payment = Create();

        Assert.True(payment.IsParticipant(RequesterId));
        Assert.True(payment.IsParticipant(CaregiverId));
        Assert.False(payment.IsParticipant(Guid.NewGuid()));
    }
}

public class PlatformFeePolicyTests
{
    /// <summary>
    /// The split must always add back to the gross. If it did not, rounding would create
    /// or destroy money on every booking.
    /// </summary>
    [Theory]
    [InlineData(128)]
    [InlineData(37.50)]
    [InlineData(0.01)]
    [InlineData(99.99)]
    [InlineData(1234.57)]
    public void A_split_always_adds_back_to_the_gross(decimal gross)
    {
        var split = new PlatformFeePolicy(
            PlatformFeePolicy.DefaultCommissionRate,
            PlatformFeePolicy.DefaultReducedCommissionRate).Split(gross);

        Assert.Equal(gross, split.PlatformFee + split.CaregiverEarnings);
    }

    [Fact]
    public void The_fee_is_the_configured_share()
    {
        var split = new PlatformFeePolicy(0.15m, 0.10m).Split(100m);

        Assert.Equal(15m, split.PlatformFee);
        Assert.Equal(85m, split.CaregiverEarnings);
    }

    [Fact]
    public void A_zero_commission_gives_the_caregiver_everything()
    {
        var split = new PlatformFeePolicy(0m, 0m).Split(100m);

        Assert.Equal(0m, split.PlatformFee);
        Assert.Equal(100m, split.CaregiverEarnings);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.51)]
    public void An_implausible_commission_rate_is_rejected(decimal rate)
    {
        Assert.Throws<DomainException>(() => new PlatformFeePolicy(rate, 0m));
    }

    /// <summary>REDUCED_COMMISSION: the caregiver keeps more of the same gross.</summary>
    [Fact]
    public void A_reduced_commission_leaves_the_caregiver_with_more()
    {
        var policy = new PlatformFeePolicy(0.15m, 0.10m);

        var standard = policy.Split(100m);
        var reduced = policy.Split(100m, hasReducedCommission: true);

        Assert.Equal(85m, standard.CaregiverEarnings);
        Assert.Equal(90m, reduced.CaregiverEarnings);

        // Still no cent created or lost.
        Assert.Equal(100m, reduced.PlatformFee + reduced.CaregiverEarnings);
    }

    [Fact]
    public void The_rate_reported_matches_the_rate_applied()
    {
        var policy = new PlatformFeePolicy(0.15m, 0.10m);

        Assert.Equal(0.15m, policy.RateFor(hasReducedCommission: false));
        Assert.Equal(0.10m, policy.RateFor(hasReducedCommission: true));
    }

    /// <summary>
    /// A misconfiguration that charged subscribers more than everyone else would be
    /// invisible in the numbers and indefensible if noticed.
    /// </summary>
    [Fact]
    public void A_reduced_rate_above_the_standard_rate_is_rejected()
    {
        Assert.Throws<DomainException>(() => new PlatformFeePolicy(0.10m, 0.15m));
    }
}

public class PayoutTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Payout Queue(decimal gross = 100m)
    {
        var policy = new PlatformFeePolicy(0.15m, 0.10m);

        return Payout.Queue(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            policy.Split(gross), policy.CommissionRate, Now, Now);
    }

    [Fact]
    public void A_queued_payout_captures_the_split_and_the_rate()
    {
        var payout = Queue(100m);

        Assert.Equal(PayoutStatus.Queued, payout.Status);
        Assert.Equal(100m, payout.GrossAmount);
        Assert.Equal(15m, payout.PlatformFee);
        Assert.Equal(85m, payout.Amount);
        // Captured so a later pricing change cannot alter an earned payout.
        Assert.Equal(0.15m, payout.CommissionRate);
    }

    [Fact]
    public void Queuing_raises_a_payout_queued_event()
    {
        Assert.IsType<GoSit.Domain.Payments.Events.PayoutQueuedDomainEvent>(
            Assert.Single(Queue().DomainEvents));
    }

    [Fact]
    public void A_payout_moves_through_in_transit_to_paid()
    {
        var payout = Queue();

        payout.MarkInTransit("tr_test_1");
        Assert.Equal(PayoutStatus.InTransit, payout.Status);

        payout.MarkPaid(Now);
        Assert.Equal(PayoutStatus.Paid, payout.Status);
        Assert.Equal(Now, payout.PaidAtUtc);
    }

    [Fact]
    public void Marking_paid_twice_is_idempotent()
    {
        var payout = Queue();
        payout.MarkInTransit("tr_test_1");
        payout.MarkPaid(Now);
        payout.ClearDomainEvents();

        payout.MarkPaid(Now.AddMinutes(1));

        Assert.Empty(payout.DomainEvents);
    }

    /// <summary>A failed transfer still owes the caregiver, so it stays retryable.</summary>
    [Fact]
    public void A_failed_payout_can_be_retried()
    {
        var payout = Queue();
        payout.MarkInTransit("tr_test_1");
        payout.MarkFailed("The account is not ready to receive transfers.");

        Assert.Equal(PayoutStatus.Failed, payout.Status);

        payout.MarkInTransit("tr_test_2");
        Assert.Equal(PayoutStatus.InTransit, payout.Status);
        Assert.Null(payout.FailureReason);
    }

    [Fact]
    public void A_paid_payout_cannot_be_failed_afterwards()
    {
        var payout = Queue();
        payout.MarkInTransit("tr_test_1");
        payout.MarkPaid(Now);

        payout.MarkFailed("Too late.");

        Assert.Equal(PayoutStatus.Paid, payout.Status);
    }

    [Fact]
    public void A_payout_with_nothing_left_for_the_caregiver_is_rejected()
    {
        var policy = new PlatformFeePolicy(0m, 0m);

        Assert.Throws<DomainException>(() => Payout.Queue(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            policy.Split(0m), policy.CommissionRate, Now, Now));
    }
}
