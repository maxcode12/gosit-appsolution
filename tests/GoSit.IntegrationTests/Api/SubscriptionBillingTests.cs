using System.Net;
using System.Net.Http.Json;
using GoSit.Application.Abstractions;
using GoSit.Domain.Subscriptions;
using GoSit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Recurring billing.
///
/// The property under test is the one that matters: <b>with billing configured, calling
/// the subscribe endpoint grants nothing.</b> A plan becomes real only when the provider
/// confirms payment over a signed webhook. Everything else here exists to make sure that
/// stays true.
///
/// Stripe itself is not called — there are no credentials in CI, and a test that needed
/// them would be a test nobody runs. A recording provider stands in at the same seam the
/// real one occupies, and the webhook path is exercised with genuinely signed payloads
/// through the real verification code.
/// </summary>
public sealed class RecordingBillingProvider : ISubscriptionBillingProvider
{
    public const string CheckoutUrl = "https://checkout.stripe.test/session/cs_test_123";

    public List<BillingCheckoutRequest> Checkouts { get; } = [];

    public List<string> Cancellations { get; } = [];

    public bool IsLive => true;

    public Task<string> EnsureCustomerAsync(
        string? existingCustomerId,
        string email,
        string fullName,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken) =>
        Task.FromResult(existingCustomerId ?? "cus_test_123");

    public Task<BillingCheckoutSession> StartCheckoutAsync(
        BillingCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        Checkouts.Add(request);

        return Task.FromResult(new BillingCheckoutSession("cs_test_123", CheckoutUrl));
    }

    public Task CancelAtPeriodEndAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        Cancellations.Add(providerSubscriptionId);

        return Task.CompletedTask;
    }

    public Task<string> CreateBillingPortalUrlAsync(
        string providerCustomerId,
        string returnUrl,
        CancellationToken cancellationToken) =>
        Task.FromResult($"https://billing.stripe.test/p/session/{providerCustomerId}");
}

/// <summary>An API host with billing switched on and a price configured for Plus.</summary>
public sealed class BillingEnabledFactory : GoSitApiFactory
{
    public RecordingBillingProvider Billing { get; } = new();

    protected override IDictionary<string, string?> SettingOverrides =>
        new Dictionary<string, string?>
        {
            // Links the plan to a provider price at seed time.
            ["Stripe:Prices:PLUS_REQUESTER"] = "price_test_plus",
        };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddScoped<ISubscriptionBillingProvider>(_ => Billing);
        });
    }
}

public class SubscriptionBillingTests
    : IClassFixture<BillingEnabledFactory>
{
    private readonly BillingEnabledFactory _factory;

    public SubscriptionBillingTests(BillingEnabledFactory factory) => _factory = factory;

    private sealed record SubscribeResultBody(string? CheckoutUrl, SubscriptionBody? Subscription);

    private sealed record SubscriptionBody(
        string PlanCode, string PlanName, string Status, IReadOnlyList<string> Features,
        DateTime? CurrentPeriodEndUtc, bool CancelAtPeriodEnd,
        decimal? AmountCharged, decimal? DiscountApplied,
        bool BillingEnabled, bool HasBillingPortal);

    private Task<HttpResponseMessage> SubscribeAsync(HttpClient client, string planCode = "PLUS_REQUESTER") =>
        client.PostAsJsonAsync("/api/v1/me/subscription", new { planCode, promoCode = (string?)null });

    private async Task<Guid> UserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    /// <summary>
    /// The central rule. Calling subscribe with billing configured hands back a checkout
    /// URL and nothing else — no plan, no features, no row.
    /// </summary>
    [Fact]
    public async Task Subscribing_returns_a_checkout_url_and_grants_nothing()
    {
        var (client, _) = await _factory.RegisterRequesterAsync();

        var response = await SubscribeAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SubscribeResultBody>();

        Assert.Equal(RecordingBillingProvider.CheckoutUrl, body!.CheckoutUrl);
        Assert.Null(body.Subscription);

        // The account is still on the free plan with no features.
        var mine = await client.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("FREE_REQUESTER", mine!.PlanCode);
        Assert.Empty(mine.Features);
        Assert.True(mine.BillingEnabled);
    }

    /// <summary>
    /// Someone who abandons checkout, then types the success URL by hand, must still
    /// have nothing. The redirect is not evidence.
    /// </summary>
    [Fact]
    public async Task Abandoning_checkout_leaves_the_account_on_the_free_plan()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        await SubscribeAsync(client);

        // No webhook arrives. Nothing changes, however many times it is asked.
        var mine = await client.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("FREE_REQUESTER", mine!.PlanCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        // Scoped to this account: the fixture is shared, so other tests in this class
        // have subscriptions of their own in the same database.
        Assert.Empty(await context.UserSubscriptions.Where(s => s.UserId == userId).ToListAsync());
    }

    /// <summary>
    /// Checkout must carry the account and plan as metadata, because that is how a
    /// webhook is matched to an account — never by anything the browser reports.
    /// </summary>
    [Fact]
    public async Task Checkout_carries_the_account_and_plan_as_metadata()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        _factory.Billing.Checkouts.Clear();
        await SubscribeAsync(client);

        var checkout = Assert.Single(_factory.Billing.Checkouts);

        Assert.Equal("price_test_plus", checkout.PriceId);
        Assert.Equal(userId.ToString(), checkout.Metadata[SubscriptionMetadataKeys.UserId]);
        Assert.Equal("PLUS_REQUESTER", checkout.Metadata[SubscriptionMetadataKeys.PlanCode]);

        // A family goes back to the family plan screen, not a caregiver one.
        Assert.Contains("/account/plan", checkout.SuccessUrl);
    }

    /// <summary>
    /// The webhook is what makes a subscription real, and it goes through the same
    /// signature verification as every other event.
    /// </summary>
    [Fact]
    public async Task A_signed_activation_webhook_starts_the_subscription()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        await SubscribeAsync(client);

        await SendWebhookAsync(StripeWebhookSigner.CheckoutCompleted(
            $"evt_{Guid.NewGuid():N}", "sub_test_1", "cus_test_123", userId, "PLUS_REQUESTER"));

        var mine = await client.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("PLUS_REQUESTER", mine!.PlanCode);
        Assert.Equal("Active", mine.Status);
        Assert.Contains(SubscriptionFeature.UnlimitedFavourites, mine.Features);
    }

    /// <summary>
    /// An unsigned webhook must change nothing. Without this, anyone who learned the URL
    /// could hand themselves a paid plan.
    /// </summary>
    [Fact]
    public async Task An_unsigned_activation_webhook_is_refused_and_grants_nothing()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        var payload = StripeWebhookSigner.CheckoutCompleted(
            $"evt_{Guid.NewGuid():N}", "sub_forged", "cus_test_123", userId, "PLUS_REQUESTER");

        var response = await _factory.CreateClient().PostAsync(
            "/api/v1/webhooks/stripe",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var mine = await client.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("FREE_REQUESTER", mine!.PlanCode);
    }

    /// <summary>Deliveries repeat. A replayed activation must not create a second row.</summary>
    [Fact]
    public async Task A_replayed_activation_webhook_is_ignored()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        await SubscribeAsync(client);

        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = StripeWebhookSigner.CheckoutCompleted(
            eventId, "sub_test_replay", "cus_test_123", userId, "PLUS_REQUESTER");

        await SendWebhookAsync(payload);
        await SendWebhookAsync(payload);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var subscriptions = await context.UserSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        Assert.Single(subscriptions);
    }

    /// <summary>
    /// A failed invoice must not cut someone off immediately — Stripe is still retrying,
    /// and entitlements continue through the grace period.
    /// </summary>
    [Fact]
    public async Task A_failed_invoice_keeps_entitlements_through_the_grace_period()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        await SubscribeAsync(client);
        await SendWebhookAsync(StripeWebhookSigner.CheckoutCompleted(
            $"evt_{Guid.NewGuid():N}", "sub_test_pastdue", "cus_test_123", userId, "PLUS_REQUESTER"));

        await SendWebhookAsync(StripeWebhookSigner.InvoicePaymentFailed(
            $"evt_{Guid.NewGuid():N}", "sub_test_pastdue", "cus_test_123", userId, "PLUS_REQUESTER"));

        var mine = await client.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("PastDue", mine!.Status);
        Assert.Contains(SubscriptionFeature.UnlimitedFavourites, mine.Features);
    }

    /// <summary>
    /// A renewal invoice extends the period, and the provider's dates win.
    ///
    /// This also exercises the fiddliest bit of parsing: Stripe moved the subscription
    /// reference off the invoice's top level and onto <c>parent.subscription_details</c>,
    /// so an integration reading the old field would silently stop applying renewals.
    /// </summary>
    [Fact]
    public async Task A_renewal_invoice_extends_the_period_using_the_providers_dates()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        await SubscribeAsync(client);
        await SendWebhookAsync(StripeWebhookSigner.CheckoutCompleted(
            $"evt_{Guid.NewGuid():N}", "sub_test_renew", "cus_test_123", userId, "PLUS_REQUESTER"));

        // A period ending well beyond anything AddMonths(1) would have produced, so the
        // assertion can only pass if Stripe's dates were adopted.
        var periodStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var periodEnd = DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds();

        await SendWebhookAsync(StripeWebhookSigner.InvoicePaid(
            $"evt_{Guid.NewGuid():N}", "sub_test_renew", "cus_test_123", userId, "PLUS_REQUESTER",
            billingReason: "subscription_cycle",
            periodStart: periodStart,
            periodEnd: periodEnd));

        var mine = await client.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("Active", mine!.Status);
        Assert.NotNull(mine.CurrentPeriodEndUtc);

        // Within a minute of what the provider said, not a month from signup.
        var expected = DateTimeOffset.FromUnixTimeSeconds(periodEnd).UtcDateTime;

        Assert.True(
            Math.Abs((mine.CurrentPeriodEndUtc!.Value - expected).TotalMinutes) < 1,
            $"Expected the provider's period end {expected:O}, got {mine.CurrentPeriodEndUtc:O}");
    }

    /// <summary>When the provider ends it, the plan is gone.</summary>
    [Fact]
    public async Task A_cancellation_webhook_removes_the_plan()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        await SubscribeAsync(client);
        await SendWebhookAsync(StripeWebhookSigner.CheckoutCompleted(
            $"evt_{Guid.NewGuid():N}", "sub_test_cancel", "cus_test_123", userId, "PLUS_REQUESTER"));

        await SendWebhookAsync(StripeWebhookSigner.SubscriptionDeleted(
            $"evt_{Guid.NewGuid():N}", "sub_test_cancel", "cus_test_123", userId, "PLUS_REQUESTER"));

        var mine = await client.GetFromJsonAsync<SubscriptionBody>("/api/v1/me/subscription");

        Assert.Equal("FREE_REQUESTER", mine!.PlanCode);
        Assert.Empty(mine.Features);
    }

    /// <summary>
    /// Cancelling stops the renewal at the provider too. Cancelling only locally would
    /// leave Stripe billing a card for a plan the person believes they ended.
    /// </summary>
    [Fact]
    public async Task Cancelling_stops_the_renewal_at_the_provider()
    {
        var (client, email) = await _factory.RegisterRequesterAsync();
        var userId = await UserIdAsync(email);

        await SubscribeAsync(client);
        await SendWebhookAsync(StripeWebhookSigner.CheckoutCompleted(
            $"evt_{Guid.NewGuid():N}", "sub_test_stop", "cus_test_123", userId, "PLUS_REQUESTER"));

        _factory.Billing.Cancellations.Clear();

        var cancelled = await client.DeleteAsync("/api/v1/me/subscription");

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Contains("sub_test_stop", _factory.Billing.Cancellations);
    }

    private async Task SendWebhookAsync(string payload)
    {
        var response = await _factory.CreateClient().PostAsync(
            "/api/v1/webhooks/stripe",
            Signed(payload));

        response.EnsureSuccessStatusCode();
    }

    private static HttpContent Signed(string payload)
    {
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", StripeWebhookSigner.Sign(payload));

        return content;
    }
}
