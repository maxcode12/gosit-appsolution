using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Growth;
using GoSit.Domain.Subscriptions;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class SubscriptionTests(GoSitApiFactory factory)
{
    private sealed record PlanBody(
        string Code, string Name, string Audience, decimal MonthlyPrice,
        string Currency, bool IsFree, IReadOnlyList<string> Features);

    private sealed record SubscriptionBody(
        string PlanCode, string PlanName, string Status, IReadOnlyList<string> Features,
        DateTime? CurrentPeriodEndUtc, bool CancelAtPeriodEnd,
        decimal? AmountCharged, decimal? DiscountApplied,
        bool BillingEnabled, bool HasBillingPortal);

    /// <summary>
    /// What POST /me/subscription returns.
    ///
    /// Two shapes: a checkout URL when billing is configured, or the started
    /// subscription when it is not. These tests run with no Stripe key, so they take the
    /// second branch — <see cref="SubscriptionBillingTests"/> covers the first.
    /// </summary>
    private sealed record SubscribeResultBody(
        string? CheckoutUrl, SubscriptionBody? Subscription);

    private sealed record ReferralBody(
        Guid Id, string Status, DateTime CreatedAtUtc, DateTime? QualifiedAtUtc, decimal? RewardAmount);

    private sealed record ReferralSummaryBody(
        string MyReferralCode, int PendingCount, int QualifiedCount,
        int RewardedCount, IReadOnlyList<ReferralBody> Referrals);

    private static Task<HttpResponseMessage> SubscribeAsync(
        HttpClient client, string planCode, string? promoCode = null) =>
        client.PostAsJsonAsync("/api/v1/me/subscription", new { planCode, promoCode });

    private async Task<PromoCode> CreatePromoAsync(
        string code,
        DiscountType type = DiscountType.Percentage,
        decimal value = 25m,
        int? maxRedemptions = null,
        PlanAudience? audience = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var promo = PromoCode.Create(
            code, "Test offer", type, value,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), maxRedemptions, audience);

        context.PromoCodes.Add(promo);
        await context.SaveChangesAsync();

        return promo;
    }

    // ---- plans ----

    [Fact]
    public async Task A_requester_only_sees_requester_plans()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var plans = await requester.GetFromJsonAsync<List<PlanBody>>("/api/v1/subscription-plans");

        Assert.NotEmpty(plans!);
        Assert.All(plans!, p => Assert.Equal("CareRequester", p.Audience));
    }

    [Fact]
    public async Task A_caregiver_only_sees_caregiver_plans()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        var plans = await caregiver.GetFromJsonAsync<List<PlanBody>>("/api/v1/subscription-plans");

        Assert.NotEmpty(plans!);
        Assert.All(plans!, p => Assert.Equal("Caregiver", p.Audience));
    }

    /// <summary>
    /// "No subscription" and "on the free plan" must be the same state, so there is no
    /// null case for a feature gate to get wrong.
    /// </summary>
    [Fact]
    public async Task An_account_with_no_subscription_is_on_the_free_plan()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var mine = await requester.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("FREE_REQUESTER", mine!.PlanCode);
        Assert.Equal("Free", mine.Status);
        Assert.Empty(mine.Features);
    }

    // ---- subscribing ----

    [Fact]
    public async Task Subscribing_grants_the_plans_features()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await SubscribeAsync(requester, "PLUS_REQUESTER");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var mine = await requester.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("PLUS_REQUESTER", mine!.PlanCode);
        Assert.Equal("Active", mine.Status);
        Assert.Contains(SubscriptionFeature.UnlimitedFavourites, mine.Features);
        Assert.NotNull(mine.CurrentPeriodEndUtc);
    }

    /// <summary>
    /// A caregiver must not be able to buy a requester plan by sending its code.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_subscribe_to_a_requester_plan()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        var response = await SubscribeAsync(caregiver, "PLUS_REQUESTER");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_free_plan_cannot_be_subscribed_to()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await SubscribeAsync(requester, "FREE_REQUESTER");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_plan_is_rejected()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await SubscribeAsync(requester, "NOT_A_PLAN");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Subscribing_twice_is_refused()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await SubscribeAsync(requester, "PLUS_REQUESTER");

        var second = await SubscribeAsync(requester, "PLUS_REQUESTER");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>Someone who has paid for a month gets the month.</summary>
    [Fact]
    public async Task Cancelling_keeps_the_features_until_the_period_ends()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await SubscribeAsync(requester, "PLUS_REQUESTER");

        var cancelled = await requester.DeleteAsync("/api/v1/me/subscription");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var mine = await requester.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.True(mine!.CancelAtPeriodEnd);
        Assert.Contains(SubscriptionFeature.UnlimitedFavourites, mine.Features);
    }

    [Fact]
    public async Task Cancelling_without_a_subscription_returns_404()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.DeleteAsync("/api/v1/me/subscription");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Entitlements are resolved from stored state, never from what the client says it
    /// is on. Sending a plan code on an unrelated request must change nothing.
    /// </summary>
    [Fact]
    public async Task A_client_cannot_assert_its_own_entitlements()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        // A hopeful client claiming Plus on the read endpoint.
        var response = await requester.GetAsync(
            "/api/v1/me/subscription?planCode=PLUS_REQUESTER&features=UNLIMITED_FAVOURITES");

        var mine = await response.Content.ReadFromJsonAsync<SubscriptionBody>();

        Assert.Equal("FREE_REQUESTER", mine!.PlanCode);
        Assert.Empty(mine.Features);
    }

    // ---- promo codes ----

    [Fact]
    public async Task A_percentage_promo_reduces_the_amount_charged()
    {
        await CreatePromoAsync("SAVE25", DiscountType.Percentage, 25m);
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await SubscribeAsync(requester, "PLUS_REQUESTER", "SAVE25");
        var body = (await response.Content.ReadFromJsonAsync<SubscribeResultBody>())!.Subscription!;

        // 25% of the 12.00 plan.
        Assert.Equal(3m, body.DiscountApplied);
        Assert.Equal(9m, body.AmountCharged);
    }

    [Fact]
    public async Task A_promo_code_is_forgiving_about_how_it_is_typed()
    {
        await CreatePromoAsync("SAVE20", DiscountType.Percentage, 20m);
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await SubscribeAsync(requester, "PLUS_REQUESTER", " save-20 ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_promo_code_is_rejected()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await SubscribeAsync(requester, "PLUS_REQUESTER", "NOTREAL");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_promo_code_past_its_redemption_cap_is_refused()
    {
        await CreatePromoAsync("ONLYONE", DiscountType.Percentage, 10m, maxRedemptions: 1);

        var first = await factory.RegisterCareRequesterAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await SubscribeAsync(first, "PLUS_REQUESTER", "ONLYONE")).StatusCode);

        var second = await factory.RegisterCareRequesterAsync();
        var response = await SubscribeAsync(second, "PLUS_REQUESTER", "ONLYONE");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// The refusal must not reveal that a code targets caregivers — that would leak a
    /// campaign's targeting to anyone who tried codes.
    /// </summary>
    [Fact]
    public async Task A_promo_for_the_other_audience_is_refused_without_saying_why()
    {
        await CreatePromoAsync(
            "CGONLY", DiscountType.Percentage, 10m, audience: PlanAudience.Caregiver);

        var requester = await factory.RegisterCareRequesterAsync();
        var response = await SubscribeAsync(requester, "PLUS_REQUESTER", "CGONLY");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("caregiver", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_fixed_discount_never_exceeds_the_price()
    {
        await CreatePromoAsync("HUGE", DiscountType.FixedAmount, 500m);
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await SubscribeAsync(requester, "PLUS_REQUESTER", "HUGE");
        var body = (await response.Content.ReadFromJsonAsync<SubscribeResultBody>())!.Subscription!;

        // Capped at the plan price: a discount must never produce a negative charge.
        Assert.Equal(12m, body.DiscountApplied);
        Assert.Equal(0m, body.AmountCharged);
    }

    // ---- referrals ----

    [Fact]
    public async Task An_account_gets_a_referral_code_on_first_view()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var summary = await requester.GetFromJsonAsync<ReferralSummaryBody>("/api/v1/me/referrals");

        Assert.Equal(ReferralCode.Length, summary!.MyReferralCode.Length);
        Assert.Empty(summary.Referrals);
    }

    /// <summary>A code must never change once someone has shared it.</summary>
    [Fact]
    public async Task A_referral_code_is_stable_across_views()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var first = await requester.GetFromJsonAsync<ReferralSummaryBody>("/api/v1/me/referrals");
        var second = await requester.GetFromJsonAsync<ReferralSummaryBody>("/api/v1/me/referrals");

        Assert.Equal(first!.MyReferralCode, second!.MyReferralCode);
    }

    [Fact]
    public async Task Applying_a_code_records_a_pending_referral_for_the_referrer()
    {
        var referrer = await factory.RegisterCareRequesterAsync();
        var code = (await referrer.GetFromJsonAsync<ReferralSummaryBody>(
            "/api/v1/me/referrals"))!.MyReferralCode;

        var referee = await factory.RegisterCareRequesterAsync();
        var applied = await referee.PostAsJsonAsync("/api/v1/me/referrals", new { code });

        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var referral = (await applied.Content.ReadFromJsonAsync<ReferralBody>())!;

        // Rewarding signups would pay for throwaway accounts, so it starts pending.
        Assert.Equal("Pending", referral.Status);

        var summary = await referrer.GetFromJsonAsync<ReferralSummaryBody>("/api/v1/me/referrals");
        Assert.Equal(1, summary!.PendingCount);
        Assert.Equal(0, summary.QualifiedCount);
    }

    /// <summary>The most obvious abuse, and the cheapest to stop.</summary>
    [Fact]
    public async Task Applying_your_own_code_is_refused()
    {
        var user = await factory.RegisterCareRequesterAsync();
        var code = (await user.GetFromJsonAsync<ReferralSummaryBody>(
            "/api/v1/me/referrals"))!.MyReferralCode;

        var response = await user.PostAsJsonAsync("/api/v1/me/referrals", new { code });

        // Reported as "not recognized" rather than "that is your own", so a code cannot
        // be confirmed as one's own by trying it.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_account_can_only_be_referred_once()
    {
        var firstReferrer = await factory.RegisterCareRequesterAsync();
        var firstCode = (await firstReferrer.GetFromJsonAsync<ReferralSummaryBody>(
            "/api/v1/me/referrals"))!.MyReferralCode;

        var secondReferrer = await factory.RegisterCareRequesterAsync();
        var secondCode = (await secondReferrer.GetFromJsonAsync<ReferralSummaryBody>(
            "/api/v1/me/referrals"))!.MyReferralCode;

        var referee = await factory.RegisterCareRequesterAsync();
        await referee.PostAsJsonAsync("/api/v1/me/referrals", new { code = firstCode });

        var second = await referee.PostAsJsonAsync("/api/v1/me/referrals", new { code = secondCode });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task An_unknown_referral_code_is_rejected()
    {
        var referee = await factory.RegisterCareRequesterAsync();

        var response = await referee.PostAsJsonAsync(
            "/api/v1/me/referrals", new { code = "ZZZZ9999" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A referrer sees that an invitation converted, not who the account belongs to.
    /// </summary>
    [Fact]
    public async Task A_referrer_learns_nothing_identifying_about_the_referee()
    {
        var referrer = await factory.RegisterCareRequesterAsync();
        var code = (await referrer.GetFromJsonAsync<ReferralSummaryBody>(
            "/api/v1/me/referrals"))!.MyReferralCode;

        var referee = await factory.RegisterCareRequesterAsync();
        await referee.PostAsJsonAsync("/api/v1/me/referrals", new { code });

        var body = await referrer.GetStringAsync("/api/v1/me/referrals");

        Assert.DoesNotContain("@example.com", body);
        Assert.DoesNotContain("refereeUserId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Subscription_and_referral_endpoints_require_authentication()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/me/subscription")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/me/referrals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/subscription-plans")).StatusCode);
    }
}
