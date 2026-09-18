using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Staff management of promo codes — the surface that did not exist, so codes had to be
/// inserted into the database by hand.
///
/// A promo code is a way of giving money away, and the tests that matter are the ones
/// about not giving it away twice or by accident: a code cannot be reissued while the
/// old one is still on file, deactivating is idempotent, and nothing here is reachable
/// without a staff account.
/// </summary>
[Collection(nameof(ApiCollection))]
public class PromoCodeAdminTests(GoSitApiFactory factory)
{
    private sealed record PromoBody(
        Guid Id,
        string Code,
        string Description,
        string DiscountType,
        decimal Value,
        string? Audience,
        DateTime ValidFromUtc,
        DateTime? ValidUntilUtc,
        int? MaxRedemptions,
        int RedemptionCount,
        bool IsActive,
        string Status);

    /// <summary>A code nobody else in the suite will collide with.</summary>
    private static string UniqueCode() => $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private static object NewCode(
        string code,
        int? maxRedemptions = null,
        string? audience = null,
        DateTime? validFrom = null,
        DateTime? validUntil = null,
        int discountType = 1,
        decimal value = 25m) =>
        new
        {
            code,
            description = "Test campaign",
            discountType,
            value,
            validFromUtc = validFrom,
            validUntilUtc = validUntil,
            maxRedemptions,
            audience,
        };

    private static async Task<PromoBody> CreateAsync(HttpClient admin, object payload)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/promo-codes", payload);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PromoBody>())!;
    }

    // ---- who can reach it ----

    [Fact]
    public async Task Promo_code_routes_require_a_staff_account()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var listed = await requester.GetAsync("/api/v1/admin/promo-codes");
        var created = await requester.PostAsJsonAsync(
            "/api/v1/admin/promo-codes", NewCode(UniqueCode()));

        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    [Fact]
    public async Task Promo_code_routes_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/admin/promo-codes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- creating ----

    [Fact]
    public async Task A_created_code_can_be_redeemed_by_a_subscriber()
    {
        var admin = await factory.RegisterAdminAsync();
        var code = UniqueCode();

        await CreateAsync(admin, NewCode(code, value: 50m));

        // The point of the whole surface: a code typed by staff works for a customer.
        var requester = await factory.RegisterCareRequesterAsync();

        var subscribed = await requester.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode = "PLUS_REQUESTER", promoCode = code });

        subscribed.EnsureSuccessStatusCode();

        var body = await subscribed.Content.ReadFromJsonAsync<SubscribeResultBody>();

        // Half off the $12 plan.
        Assert.Equal(6m, body!.Subscription!.DiscountApplied);
        Assert.Equal(6m, body.Subscription.AmountCharged);
    }

    private sealed record SubscribeResultBody(string? CheckoutUrl, SubscriptionBody? Subscription);

    private sealed record SubscriptionBody(decimal? AmountCharged, decimal? DiscountApplied);

    /// <summary>Omitting a start date means "start now", not "refuse".</summary>
    [Fact]
    public async Task A_code_with_no_start_date_is_live_immediately()
    {
        var admin = await factory.RegisterAdminAsync();

        var created = await CreateAsync(admin, NewCode(UniqueCode()));

        Assert.Equal("Live", created.Status);
        Assert.True(created.IsActive);
    }

    /// <summary>
    /// The case someone actually hits: reissuing last year's campaign. A database
    /// uniqueness error would tell them nothing about *why* the code is unavailable.
    /// </summary>
    [Fact]
    public async Task Reusing_an_existing_code_is_refused_by_name()
    {
        var admin = await factory.RegisterAdminAsync();
        var code = UniqueCode();

        await CreateAsync(admin, NewCode(code));

        var second = await admin.PostAsJsonAsync("/api/v1/admin/promo-codes", NewCode(code));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var problem = await second.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Contains(code, problem!.Title, StringComparison.Ordinal);
    }

    private sealed record ProblemBody(string Title);

    /// <summary>Codes are normalised, so casing cannot smuggle a duplicate past the check.</summary>
    [Fact]
    public async Task A_code_differing_only_in_case_is_the_same_code()
    {
        var admin = await factory.RegisterAdminAsync();
        var code = UniqueCode();

        await CreateAsync(admin, NewCode(code));

        var second = await admin.PostAsJsonAsync(
            "/api/v1/admin/promo-codes", NewCode(code.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    // Over 100 percent.
    [InlineData(1, 150)]
    // Not a discount at all.
    [InlineData(1, 0)]
    [InlineData(2, -5)]
    public async Task An_impossible_discount_is_refused(int discountType, decimal value)
    {
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsJsonAsync(
            "/api/v1/admin/promo-codes",
            NewCode(UniqueCode(), discountType: discountType, value: value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_code_that_expires_before_it_starts_is_refused()
    {
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsJsonAsync(
            "/api/v1/admin/promo-codes",
            NewCode(
                UniqueCode(),
                validFrom: DateTime.UtcNow.AddDays(10),
                validUntil: DateTime.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- the list ----

    /// <summary>
    /// The question staff arrive with is "how much of this campaign is left", so the
    /// listing has to carry the budget and a status that accounts for every way a code
    /// stops working — not a bare active flag that reads "active" on an expired code.
    /// </summary>
    [Fact]
    public async Task The_listing_reports_the_budget_and_a_real_status()
    {
        var admin = await factory.RegisterAdminAsync();
        var code = UniqueCode();

        await CreateAsync(admin, NewCode(code, maxRedemptions: 1));

        var requester = await factory.RegisterCareRequesterAsync();

        (await requester.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode = "PLUS_REQUESTER", promoCode = code }))
            .EnsureSuccessStatusCode();

        var listed = await admin.GetFromJsonAsync<List<PromoBody>>("/api/v1/admin/promo-codes");
        var found = listed!.Single(c => c.Code == code);

        Assert.Equal(1, found.RedemptionCount);
        Assert.Equal(1, found.MaxRedemptions);

        // Still IsActive, but spent — which is why a bare flag is not enough.
        Assert.True(found.IsActive);
        Assert.Equal("FullyRedeemed", found.Status);
    }

    [Fact]
    public async Task A_scheduled_code_is_listed_as_scheduled_rather_than_live()
    {
        var admin = await factory.RegisterAdminAsync();
        var code = UniqueCode();

        await CreateAsync(admin, NewCode(code, validFrom: DateTime.UtcNow.AddDays(7)));

        var listed = await admin.GetFromJsonAsync<List<PromoBody>>("/api/v1/admin/promo-codes");

        Assert.Equal("Scheduled", listed!.Single(c => c.Code == code).Status);
    }

    // ---- switching one off ----

    [Fact]
    public async Task A_deactivated_code_can_no_longer_be_redeemed()
    {
        var admin = await factory.RegisterAdminAsync();
        var code = UniqueCode();

        var created = await CreateAsync(admin, NewCode(code));

        var deactivated = await admin.PostAsync(
            $"/api/v1/admin/promo-codes/{created.Id}/deactivate", null);

        deactivated.EnsureSuccessStatusCode();
        Assert.Equal("Deactivated", (await deactivated.Content.ReadFromJsonAsync<PromoBody>())!.Status);

        var requester = await factory.RegisterCareRequesterAsync();

        var subscribed = await requester.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode = "PLUS_REQUESTER", promoCode = code });

        Assert.Equal(HttpStatusCode.Conflict, subscribed.StatusCode);
    }

    /// <summary>
    /// Two people switching off the same campaign must not see a failure — the end state
    /// they wanted already holds.
    /// </summary>
    [Fact]
    public async Task Deactivating_twice_succeeds()
    {
        var admin = await factory.RegisterAdminAsync();
        var created = await CreateAsync(admin, NewCode(UniqueCode()));

        var first = await admin.PostAsync($"/api/v1/admin/promo-codes/{created.Id}/deactivate", null);
        var second = await admin.PostAsync($"/api/v1/admin/promo-codes/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    /// <summary>
    /// Deactivation is not deletion. Redemptions point at the code, and a campaign that
    /// cost real money must stay in the record.
    /// </summary>
    [Fact]
    public async Task A_deactivated_code_stays_in_the_listing()
    {
        var admin = await factory.RegisterAdminAsync();
        var code = UniqueCode();

        var created = await CreateAsync(admin, NewCode(code));
        await admin.PostAsync($"/api/v1/admin/promo-codes/{created.Id}/deactivate", null);

        var listed = await admin.GetFromJsonAsync<List<PromoBody>>("/api/v1/admin/promo-codes");

        Assert.Contains(listed!, c => c.Code == code && !c.IsActive);
    }

    [Fact]
    public async Task Deactivating_a_code_that_does_not_exist_is_not_found()
    {
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsync(
            $"/api/v1/admin/promo-codes/{Guid.NewGuid()}/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
