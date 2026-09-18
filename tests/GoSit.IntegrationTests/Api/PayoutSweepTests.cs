using System.Net;
using System.Net.Http.Json;
using GoSit.Application.Abstractions;
using GoSit.Domain.Payouts;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Records transfers instead of sending them, and can be told to fail.
///
/// Substituted for the whole <see cref="IPaymentProvider"/> so the sweep exercises the
/// real repository, the real aggregate and the real scheduling code — only the outbound
/// call to Stripe is replaced.
/// </summary>
public sealed class RecordingPaymentProvider : IPaymentProvider
{
    public sealed record Transfer(string Destination, decimal Amount, string IdempotencyKey);

    public List<Transfer> Transfers { get; } = [];

    /// <summary>Set to make the next transfers throw, as a provider outage would.</summary>
    public bool FailTransfers { get; set; }

    /// <summary>
    /// Never reset, unlike <see cref="Transfers"/>, which tests clear between phases.
    /// Deriving the reference from the list's length produced duplicate transfer ids
    /// after a clear, and the lookup that matches a webhook to a payout expects them to
    /// be unique — which it is right to expect.
    /// </summary>
    private int _issued;

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentIntentResult($"pi_rec_{idempotencyKey}", "secret"));

    public Task<ProviderRefundResult> RefundAsync(
        string providerPaymentIntentId,
        decimal amount,
        string? reason,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderRefundResult("re_rec_1", amount));

    public Task<ProviderTransferResult> CreateTransferAsync(
        string destinationAccountId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (FailTransfers)
        {
            throw new InvalidOperationException("The provider is unavailable.");
        }

        Transfers.Add(new Transfer(destinationAccountId, amount, idempotencyKey));

        return Task.FromResult(new ProviderTransferResult($"tr_rec_{Interlocked.Increment(ref _issued)}"));
    }
}

public sealed class PayoutFactoryFixture : GoSitApiFactory
{
    public RecordingPaymentProvider Payments { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddScoped<IPaymentProvider>(_ => Payments);
        });
    }
}

/// <summary>
/// Sending caregivers what they have earned.
///
/// This is the only thing in GoSit that moves money *out*, so the rules it follows are
/// worth proving rather than trusting: money goes only to an account the provider says
/// is ready, a failure leaves the amount owed, and a retry cannot pay someone twice.
/// </summary>
public class PayoutSweepTests(PayoutFactoryFixture factory) : IClassFixture<PayoutFactoryFixture>
{
    private sealed record PayoutAccountBody(
        bool BillingConfigured, bool HasAccount, bool DetailsSubmitted,
        bool PayoutsEnabled, string? DisabledReason, decimal PendingBalance, string Currency);

    /// <summary>
    /// Queues a payout for a caregiver, going through the real booking and payment flow
    /// so the payout is created the way production creates it.
    /// </summary>
    /// <param name="available">
    /// Whether to bring the dispute window forward so the sweep will act.
    ///
    /// A real payout waits fourteen days. Most tests here are about the sweep, the retry
    /// backoff, or the provider call, so they take the money as available; the tests that
    /// are about the wait itself pass false and assert on it.
    /// </param>
    private async Task<(Guid CaregiverProfileId, Guid PayoutId)> QueueEarningsAsync(
        bool available = true)
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);
        var booking = await requester.CreateBookingAsync(
            caregiverId, recipientId, start, start.AddHours(4));

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - start);

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null))
            .EnsureSuccessStatusCode();
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null))
            .EnsureSuccessStatusCode();

        (await requester.PostAsync($"/api/v1/bookings/{booking.Id}/payment-intent", null))
            .EnsureSuccessStatusCode();

        // The provider intent id is deliberately not in the response — a client has no
        // use for it — so it is read from storage, which is where the webhook handler
        // will match on it too.
        var (intentId, amountCents) = await PaymentReferenceAsync(booking.Id);

        // A payout exists only once the money has actually landed, so the payment is
        // settled the only way it can be: a signed webhook.
        var payload = StripeWebhookSigner.PaymentSucceeded(
            $"evt_{Guid.NewGuid():N}", intentId, amountCents);

        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", StripeWebhookSigner.Sign(payload));

        (await factory.CreateClient().PostAsync("/api/v1/webhooks/stripe", content))
            .EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var payout = await context.Payouts
            .SingleAsync(p => p.CareBookingId == booking.Id);

        if (available)
        {
            await MakeAvailableAsync(payout.Id);
        }

        return (caregiverId, payout.Id);
    }

    private async Task<(string IntentId, long AmountCents)> PaymentReferenceAsync(Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var payment = await context.Payments
            .AsNoTracking()
            .Where(p => p.CareBookingId == bookingId)
            .Select(p => new { p.ProviderPaymentIntentId, p.Amount })
            .SingleAsync();

        return (payment.ProviderPaymentIntentId!, (long)(payment.Amount * 100m));
    }

    /// <summary>Marks a caregiver's connected account ready, as Stripe's webhook would.</summary>
    private async Task EnablePayoutsAsync(Guid caregiverProfileId, string accountId = "acct_test_1")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var profile = await context.CaregiverProfiles.SingleAsync(p => p.Id == caregiverProfileId);

        profile.LinkPayoutAccount($"{accountId}_{caregiverProfileId:N}"[..24], DateTime.UtcNow);
        profile.UpdatePayoutCapability(true, true, null, DateTime.UtcNow);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Brings a payout's dispute window into the past.
    ///
    /// Written straight to the database because there is no legitimate way to shorten the
    /// window — which is the point of it. The rule itself is covered by PayoutHoldTests;
    /// this only lets the sweep tests reach the code after it.
    /// </summary>
    private async Task MakeAvailableAsync(Guid payoutId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.Payouts
            .Where(p => p.Id == payoutId)
            .ExecuteUpdateAsync(p => p.SetProperty(
                x => x.AvailableAtUtc, DateTime.UtcNow.AddMinutes(-1)));
    }

    private Task<PayoutSweepResult> SweepAsync() =>
        factory.Services.GetRequiredService<PayoutSweeper>().SweepAsync(CancellationToken.None);

    private async Task<Payout> ReadPayoutAsync(Guid payoutId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Payouts.AsNoTracking().SingleAsync(p => p.Id == payoutId);
    }

    /// <summary>
    /// The rule that matters most. A caregiver who has not finished onboarding has no
    /// account that can receive money, so nothing is sent — the earnings simply wait.
    /// </summary>
    [Fact]
    public async Task Nothing_is_sent_to_a_caregiver_who_has_not_onboarded()
    {
        var (_, payoutId) = await QueueEarningsAsync();

        factory.Payments.Transfers.Clear();
        await SweepAsync();

        var payout = await ReadPayoutAsync(payoutId);

        Assert.Equal(PayoutStatus.Queued, payout.Status);
        Assert.Empty(factory.Payments.Transfers);
    }

    [Fact]
    public async Task Earnings_are_sent_once_the_account_is_ready()
    {
        var (caregiverId, payoutId) = await QueueEarningsAsync();
        await EnablePayoutsAsync(caregiverId);

        factory.Payments.Transfers.Clear();
        await SweepAsync();

        var payout = await ReadPayoutAsync(payoutId);

        Assert.Equal(PayoutStatus.InTransit, payout.Status);
        Assert.NotNull(payout.ProviderTransferId);

        var transfer = Assert.Single(
            factory.Payments.Transfers, t => t.Amount == payout.Amount);

        // Keyed on the payout, so a retry after a crash reuses the provider's original
        // transfer instead of sending the money twice.
        Assert.Equal($"payout:{payoutId}", transfer.IdempotencyKey);
    }

    /// <summary>
    /// The sweep runs every fifteen minutes forever. A payout already handed to the
    /// provider must never be handed over again.
    /// </summary>
    [Fact]
    public async Task A_payout_already_in_transit_is_not_sent_again()
    {
        var (caregiverId, payoutId) = await QueueEarningsAsync();
        await EnablePayoutsAsync(caregiverId);

        await SweepAsync();

        factory.Payments.Transfers.Clear();
        await SweepAsync();

        Assert.Empty(factory.Payments.Transfers);

        var payout = await ReadPayoutAsync(payoutId);
        Assert.Equal(PayoutStatus.InTransit, payout.Status);
    }

    /// <summary>A provider outage leaves the money owed, not lost.</summary>
    [Fact]
    public async Task A_failed_transfer_leaves_the_payout_owed_and_retryable()
    {
        var (caregiverId, payoutId) = await QueueEarningsAsync();
        await EnablePayoutsAsync(caregiverId);

        factory.Payments.FailTransfers = true;

        try
        {
            await SweepAsync();
        }
        finally
        {
            factory.Payments.FailTransfers = false;
        }

        var payout = await ReadPayoutAsync(payoutId);

        Assert.Equal(PayoutStatus.Failed, payout.Status);
        Assert.Equal(1, payout.AttemptCount);

        // The attempt was recorded despite the call throwing, so the backoff applies.
        Assert.NotNull(payout.LastAttemptAtUtc);
        Assert.NotNull(payout.FailureReason);
    }

    /// <summary>
    /// A payout is marked paid only when the provider says so — the same rule as a
    /// payment. Handing it to the provider is not evidence it arrived.
    /// </summary>
    [Fact]
    public async Task A_payout_is_only_paid_when_the_provider_confirms_it()
    {
        var (caregiverId, payoutId) = await QueueEarningsAsync();
        await EnablePayoutsAsync(caregiverId);

        await SweepAsync();

        var inTransit = await ReadPayoutAsync(payoutId);
        Assert.Equal(PayoutStatus.InTransit, inTransit.Status);

        var payload = StripeWebhookSigner.TransferPaid(
            $"evt_{Guid.NewGuid():N}",
            inTransit.ProviderTransferId!,
            (long)(inTransit.Amount * 100m));

        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", StripeWebhookSigner.Sign(payload));

        (await factory.CreateClient().PostAsync("/api/v1/webhooks/stripe", content))
            .EnsureSuccessStatusCode();

        var paid = await ReadPayoutAsync(payoutId);

        Assert.Equal(PayoutStatus.Paid, paid.Status);
        Assert.NotNull(paid.PaidAtUtc);
    }

    /// <summary>
    /// Money in flight is not money received. Showing it as paid would tell a caregiver
    /// their earnings have arrived when they have not.
    /// </summary>
    [Fact]
    public async Task Pending_balance_counts_everything_not_yet_settled()
    {
        var (caregiverId, payoutId) = await QueueEarningsAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var queued = await caregiver.GetFromJsonAsync<PayoutAccountBody>(
            "/api/v1/caregivers/me/payout-account");

        var payout = await ReadPayoutAsync(payoutId);
        Assert.Equal(payout.Amount, queued!.PendingBalance);

        await EnablePayoutsAsync(caregiverId);
        await SweepAsync();

        var inTransit = await caregiver.GetFromJsonAsync<PayoutAccountBody>(
            "/api/v1/caregivers/me/payout-account");

        Assert.Equal(payout.Amount, inTransit!.PendingBalance);
    }

    /// <summary>
    /// With no provider configured — this test host — there is no onboarding to offer,
    /// and the screen says so rather than linking somewhere that does not exist.
    /// </summary>
    [Fact]
    public async Task Onboarding_is_refused_when_no_provider_is_configured()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var status = await caregiver.GetFromJsonAsync<PayoutAccountBody>(
            "/api/v1/caregivers/me/payout-account");

        Assert.False(status!.BillingConfigured);
        Assert.False(status.HasAccount);

        var started = await caregiver.PostAsync("/api/v1/caregivers/me/payout-account", null);

        Assert.Equal(HttpStatusCode.Conflict, started.StatusCode);
    }

    [Fact]
    public async Task A_care_requester_cannot_reach_the_payout_endpoints()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var status = await requester.GetAsync("/api/v1/caregivers/me/payout-account");

        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
    }
/// <summary>
    /// The hole this closes.
    ///
    /// A payout used to go out within fifteen minutes of the payment settling, while the
    /// family had fourteen days to dispute — so by the time anyone did, the money had
    /// already left and there was nothing to refund from.
    /// </summary>
    [Fact]
    public async Task A_payout_is_not_sent_while_the_family_can_still_dispute()
    {
        var (caregiverId, payoutId) = await QueueEarningsAsync(available: false);
        await EnablePayoutsAsync(caregiverId);

        var result = await SweepAsync();

        Assert.Equal(0, result.Sent);
        Assert.Equal(PayoutStatus.Queued, (await ReadPayoutAsync(payoutId)).Status);
    }

    /// <summary>
    /// And a dispute stops it for good, not just until the window closes. The sweep must
    /// not quietly pay out an argument nobody has settled.
    /// </summary>
    [Fact]
    public async Task A_disputed_booking_holds_the_payout_past_the_window()
    {
        var (caregiverId, payoutId) = await QueueEarningsAsync(available: false);
        await EnablePayoutsAsync(caregiverId);

        var bookingId = (await ReadPayoutAsync(payoutId)).CareBookingId;

        var requester = await RequesterForBookingAsync(bookingId);

        (await requester.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/dispute",
            new { reason = "The caregiver left two hours early." }))
            .EnsureSuccessStatusCode();

        // Even with the window brought forward, the dispute keeps the money still.
        await MakeAvailableAsync(payoutId);

        var result = await SweepAsync();

        Assert.Equal(0, result.Sent);

        var payout = await ReadPayoutAsync(payoutId);

        Assert.True(payout.IsHeldForDispute);
        Assert.Equal(PayoutStatus.Queued, payout.Status);
    }

    /// <summary>The signed-in requester who booked, so a dispute can be raised as them.</summary>
    private async Task<HttpClient> RequesterForBookingAsync(Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var email = await context.CareBookings
            .Where(b => b.Id == bookingId)
            .Join(
                context.CareRequesterProfiles,
                b => b.CareRequesterProfileId,
                p => p.Id,
                (b, p) => p.UserId)
            .Join(context.Users, userId => userId, u => u.Id, (userId, u) => u.Email)
            .SingleAsync();

        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "correct-horse-battery-staple",
        });

        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<ProfileTestClient.Tokens>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }
}
