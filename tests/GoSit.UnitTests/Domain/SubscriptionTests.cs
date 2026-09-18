using GoSit.Domain.Common;
using GoSit.Domain.Growth;
using GoSit.Domain.Subscriptions;
using GoSit.Domain.Subscriptions.Events;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class SubscriptionPlanTests
{
    [Fact]
    public void A_plan_exposes_the_features_it_includes()
    {
        var plan = SubscriptionPlan.Create(
            "plus_requester", "GoSit Plus", PlanAudience.CareRequester, 12m, 2,
            [SubscriptionFeature.UnlimitedFavourites, SubscriptionFeature.AdvancedSearchFilters]);

        Assert.Equal("PLUS_REQUESTER", plan.Code);
        Assert.True(plan.Includes(SubscriptionFeature.UnlimitedFavourites));
        Assert.False(plan.Includes(SubscriptionFeature.ProfilePromotion));
        Assert.False(plan.IsFree);
    }

    [Fact]
    public void A_zero_priced_plan_is_free()
    {
        var plan = SubscriptionPlan.Create(
            "free", "Free", PlanAudience.CareRequester, 0m, 1, []);

        Assert.True(plan.IsFree);
    }

    [Fact]
    public void A_price_that_is_not_a_whole_number_of_cents_is_rejected()
    {
        Assert.Throws<DomainException>(() => SubscriptionPlan.Create(
            "bad", "Bad", PlanAudience.CareRequester, 9.999m, 1, []));
    }

    [Fact]
    public void A_negative_price_is_rejected()
    {
        Assert.Throws<DomainException>(() => SubscriptionPlan.Create(
            "bad", "Bad", PlanAudience.CareRequester, -1m, 1, []));
    }
}

public class UserSubscriptionTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private static SubscriptionPlan PaidPlan() => SubscriptionPlan.Create(
        "PLUS", "Plus", PlanAudience.CareRequester, 12m, 2,
        [SubscriptionFeature.UnlimitedFavourites]);

    private static UserSubscription Start() => UserSubscription.Start(UserId, PaidPlan(), Now);

    [Fact]
    public void A_new_subscription_is_active_for_a_month()
    {
        var subscription = Start();

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(Now, subscription.CurrentPeriodStartUtc);
        Assert.Equal(Now.AddMonths(1), subscription.CurrentPeriodEndUtc);
        Assert.True(subscription.IsActiveAt(Now));
        Assert.True(subscription.IsActiveAt(Now.AddDays(29)));
    }

    [Fact]
    public void Starting_raises_a_subscription_started_event()
    {
        Assert.IsType<SubscriptionStartedDomainEvent>(Assert.Single(Start().DomainEvents));
    }

    /// <summary>
    /// The free plan is the absence of a subscription. Storing one would create two ways
    /// to express the same state.
    /// </summary>
    [Fact]
    public void The_free_plan_cannot_be_subscribed_to()
    {
        var free = SubscriptionPlan.Create("FREE", "Free", PlanAudience.CareRequester, 0m, 1, []);

        Assert.Throws<DomainException>(() => UserSubscription.Start(UserId, free, Now));
    }

    [Fact]
    public void Entitlements_stop_when_the_period_runs_out()
    {
        var subscription = Start();

        Assert.False(subscription.IsActiveAt(Now.AddMonths(1)));
        Assert.False(subscription.IsActiveAt(Now.AddMonths(2)));
    }

    // ---- past due ----

    /// <summary>
    /// Cutting a family off from the caregivers they rely on the instant a card expires
    /// would be the wrong trade, so a grace period applies.
    /// </summary>
    [Fact]
    public void A_past_due_subscription_keeps_entitlements_through_the_grace_period()
    {
        var subscription = Start();

        subscription.MarkPastDue(Now.AddMonths(1));

        Assert.True(subscription.IsActiveAt(Now.AddMonths(1).AddDays(3)));
        Assert.False(subscription.IsActiveAt(
            Now.AddMonths(1) + UserSubscription.PastDueGrace + TimeSpan.FromDays(1)));
    }

    /// <summary>Repeated failure notices must not keep resetting the grace clock.</summary>
    [Fact]
    public void Marking_past_due_twice_does_not_extend_the_grace()
    {
        var subscription = Start();
        var firstFailure = Now.AddMonths(1);

        subscription.MarkPastDue(firstFailure);
        subscription.MarkPastDue(firstFailure.AddDays(5));

        Assert.Equal(firstFailure, subscription.PastDueSinceUtc);
    }

    [Fact]
    public void Renewing_clears_past_due()
    {
        var subscription = Start();
        subscription.MarkPastDue(Now.AddMonths(1));

        subscription.Renew(Now.AddMonths(1).AddDays(1));

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.PastDueSinceUtc);
    }

    // ---- renewal ----

    /// <summary>
    /// A late renewal must not silently shorten what was paid for, so the new period
    /// runs from the old end rather than from now.
    /// </summary>
    [Fact]
    public void Renewal_extends_from_the_period_end_not_from_now()
    {
        var subscription = Start();
        var periodEnd = subscription.CurrentPeriodEndUtc;

        subscription.Renew(periodEnd.AddDays(2));

        Assert.Equal(periodEnd, subscription.CurrentPeriodStartUtc);
        Assert.Equal(periodEnd.AddMonths(1), subscription.CurrentPeriodEndUtc);
    }

    [Fact]
    public void A_cancelled_subscription_cannot_renew()
    {
        var subscription = Start();
        subscription.CancelAtEndOfPeriod(Now.AddDays(2));

        Assert.Throws<DomainException>(() => subscription.Renew(subscription.CurrentPeriodEndUtc));
    }

    // ---- cancellation ----

    /// <summary>Someone who has paid for a month gets the month.</summary>
    [Fact]
    public void Cancelling_keeps_entitlements_until_the_period_ends()
    {
        var subscription = Start();

        subscription.CancelAtEndOfPeriod(Now.AddDays(2));

        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.True(subscription.IsActiveAt(Now.AddDays(20)));
        Assert.False(subscription.IsActiveAt(Now.AddMonths(1).AddDays(1)));
    }

    [Fact]
    public void Cancelling_raises_an_event_naming_when_it_takes_effect()
    {
        var subscription = Start();
        subscription.ClearDomainEvents();

        subscription.CancelAtEndOfPeriod(Now.AddDays(2));

        var raised = Assert.IsType<SubscriptionCancelledDomainEvent>(
            Assert.Single(subscription.DomainEvents));
        Assert.Equal(subscription.CurrentPeriodEndUtc, raised.EffectiveAtUtc);
    }

    [Fact]
    public void Cancelling_immediately_ends_entitlements_now()
    {
        var subscription = Start();

        subscription.CancelImmediately(Now.AddDays(2));

        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.False(subscription.IsActiveAt(Now.AddDays(2)));
    }

    // ---- expiry sweep ----

    [Fact]
    public void The_sweep_closes_out_a_cancelled_subscription_after_its_period()
    {
        var subscription = Start();
        subscription.CancelAtEndOfPeriod(Now.AddDays(2));

        var expired = subscription.ExpireIfDue(subscription.CurrentPeriodEndUtc.AddMinutes(1));

        Assert.True(expired);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
    }

    [Fact]
    public void The_sweep_expires_a_subscription_past_its_grace()
    {
        var subscription = Start();
        subscription.MarkPastDue(Now.AddMonths(1));

        var expired = subscription.ExpireIfDue(
            Now.AddMonths(1) + UserSubscription.PastDueGrace + TimeSpan.FromDays(1));

        Assert.True(expired);
        Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
    }

    [Fact]
    public void The_sweep_leaves_an_active_subscription_alone()
    {
        var subscription = Start();

        Assert.False(subscription.ExpireIfDue(Now.AddDays(10)));
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public void The_sweep_is_idempotent()
    {
        var subscription = Start();
        subscription.MarkPastDue(Now.AddMonths(1));
        var wellPast = Now.AddMonths(3);

        Assert.True(subscription.ExpireIfDue(wellPast));
        Assert.False(subscription.ExpireIfDue(wellPast));
    }
}

public class ReferralTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Referrer = Guid.NewGuid();
    private static readonly Guid Referee = Guid.NewGuid();

    private static Referral Record() => Referral.Record(Referrer, Referee, "abcd2345", Now);

    [Fact]
    public void A_new_referral_is_pending_and_normalizes_the_code()
    {
        var referral = Record();

        Assert.Equal(ReferralStatus.Pending, referral.Status);
        Assert.Equal("ABCD2345", referral.Code);
    }

    /// <summary>The most obvious abuse, and the cheapest to stop.</summary>
    [Fact]
    public void Referring_yourself_is_rejected()
    {
        Assert.Throws<DomainException>(() => Referral.Record(Referrer, Referrer, "ABCD2345", Now));
    }

    /// <summary>
    /// Rewarding signups would pay for throwaway accounts, so a referral qualifies only
    /// when the referee actually completes a booking.
    /// </summary>
    [Fact]
    public void A_referral_qualifies_on_a_completed_booking_not_on_signup()
    {
        var referral = Record();

        Assert.Equal(ReferralStatus.Pending, referral.Status);
        Assert.Null(referral.QualifiedAtUtc);

        Assert.True(referral.Qualify(Now.AddDays(3)));
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
    }

    [Fact]
    public void Qualifying_raises_an_event()
    {
        var referral = Record();

        referral.Qualify(Now.AddDays(3));

        Assert.IsType<ReferralQualifiedDomainEvent>(Assert.Single(referral.DomainEvents));
    }

    [Fact]
    public void Qualifying_twice_is_a_no_op()
    {
        var referral = Record();
        referral.Qualify(Now.AddDays(3));
        referral.ClearDomainEvents();

        Assert.False(referral.Qualify(Now.AddDays(4)));
        Assert.Empty(referral.DomainEvents);
    }

    [Fact]
    public void Only_a_qualified_referral_can_be_rewarded()
    {
        var referral = Record();

        Assert.Throws<DomainException>(() => referral.Reward(10m, Now));
    }

    [Fact]
    public void Rewarding_records_the_amount()
    {
        var referral = Record();
        referral.Qualify(Now.AddDays(3));

        referral.Reward(10m, Now.AddDays(4));

        Assert.Equal(ReferralStatus.Rewarded, referral.Status);
        Assert.Equal(10m, referral.RewardAmount);
    }

    [Fact]
    public void A_pending_referral_can_be_voided_but_a_rewarded_one_cannot()
    {
        var pending = Record();
        pending.Void();
        Assert.Equal(ReferralStatus.Void, pending.Status);

        var rewarded = Record();
        rewarded.Qualify(Now);
        rewarded.Reward(10m, Now);

        Assert.Throws<DomainException>(rewarded.Void);
    }

    [Fact]
    public void Generated_codes_avoid_confusable_characters()
    {
        for (var i = 0; i < 50; i++)
        {
            var code = ReferralCode.Generate();

            Assert.Equal(ReferralCode.Length, code.Length);
            // No O/0, I/1 to confuse when read aloud or off a screen.
            Assert.DoesNotContain('O', code);
            Assert.DoesNotContain('I', code);
            Assert.DoesNotContain('0', code);
            Assert.DoesNotContain('1', code);
        }
    }

    [Fact]
    public void Codes_are_forgiving_about_how_they_are_typed()
    {
        Assert.Equal("ABCD2345", ReferralCode.Normalize(" abcd-2345 "));
        Assert.Equal("ABCD2345", ReferralCode.Normalize("ABCD 2345"));
    }
}

public class PromoCodeTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    private static PromoCode Percentage(int percent = 25, int? cap = null) =>
        PromoCode.Create(
            "WELCOME25", "Welcome offer", DiscountType.Percentage, percent,
            Now.AddDays(-1), Now.AddDays(30), cap, null);

    private static PromoCode Fixed(decimal amount = 5m) =>
        PromoCode.Create(
            "FIVEOFF", "Five off", DiscountType.FixedAmount, amount,
            Now.AddDays(-1), Now.AddDays(30), null, null);

    [Fact]
    public void A_percentage_discount_is_applied_to_the_price()
    {
        Assert.Equal(3m, Percentage().DiscountOn(12m));
    }

    [Fact]
    public void A_fixed_discount_is_the_stated_amount()
    {
        Assert.Equal(5m, Fixed().DiscountOn(12m));
    }

    /// <summary>A discount must never produce a negative charge.</summary>
    [Fact]
    public void A_discount_is_capped_at_the_price()
    {
        Assert.Equal(12m, Fixed(50m).DiscountOn(12m));
        Assert.Equal(0m, Fixed(50m).DiscountOn(0m));
    }

    [Fact]
    public void A_percentage_discount_rounds_to_cents()
    {
        // 33% of 9.99 is 3.2967.
        var code = PromoCode.Create(
            "THIRTYTHREE", "33 off", DiscountType.Percentage, 33m,
            Now.AddDays(-1), null, null, null);

        Assert.Equal(3.30m, code.DiscountOn(9.99m));
    }

    [Fact]
    public void An_active_code_is_accepted()
    {
        Assert.Null(Percentage().RejectionReason(Now, PlanAudience.CareRequester));
    }

    [Fact]
    public void A_code_before_its_start_date_is_refused()
    {
        var code = PromoCode.Create(
            "FUTURE", "Later", DiscountType.Percentage, 10m,
            Now.AddDays(5), null, null, null);

        Assert.NotNull(code.RejectionReason(Now, PlanAudience.CareRequester));
    }

    [Fact]
    public void An_expired_code_is_refused()
    {
        Assert.NotNull(Percentage().RejectionReason(Now.AddDays(60), PlanAudience.CareRequester));
    }

    [Fact]
    public void A_deactivated_code_is_refused()
    {
        var code = Percentage();
        code.Deactivate();

        Assert.NotNull(code.RejectionReason(Now, PlanAudience.CareRequester));
    }

    [Fact]
    public void A_code_at_its_redemption_cap_is_refused()
    {
        var code = Percentage(cap: 2);

        code.Redeem(Now, PlanAudience.CareRequester);
        code.Redeem(Now, PlanAudience.CareRequester);

        Assert.Equal(2, code.RedemptionCount);
        Assert.NotNull(code.RejectionReason(Now, PlanAudience.CareRequester));
        Assert.Throws<DomainException>(() => code.Redeem(Now, PlanAudience.CareRequester));
    }

    /// <summary>
    /// The refusal must not say the code is for caregivers — that would leak a
    /// campaign's targeting to anyone who tried a code.
    /// </summary>
    [Fact]
    public void A_code_for_the_other_audience_is_refused_without_saying_why()
    {
        var code = PromoCode.Create(
            "CAREGIVERS", "For caregivers", DiscountType.Percentage, 10m,
            Now.AddDays(-1), null, null, PlanAudience.Caregiver);

        var reason = code.RejectionReason(Now, PlanAudience.CareRequester);

        Assert.NotNull(reason);
        Assert.DoesNotContain("caregiver", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DiscountType.Percentage, 0)]
    [InlineData(DiscountType.Percentage, 101)]
    [InlineData(DiscountType.FixedAmount, -5)]
    public void An_invalid_discount_is_rejected(DiscountType type, decimal value)
    {
        Assert.Throws<DomainException>(() => PromoCode.Create(
            "BAD", "Bad", type, value, Now, null, null, null));
    }

    [Fact]
    public void A_fixed_discount_with_sub_cent_precision_is_rejected()
    {
        Assert.Throws<DomainException>(() => PromoCode.Create(
            "BAD", "Bad", DiscountType.FixedAmount, 1.005m, Now, null, null, null));
    }

    [Fact]
    public void An_expiry_before_the_start_is_rejected()
    {
        Assert.Throws<DomainException>(() => PromoCode.Create(
            "BAD", "Bad", DiscountType.Percentage, 10m, Now, Now.AddDays(-1), null, null));
    }
}
