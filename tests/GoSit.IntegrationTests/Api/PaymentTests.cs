using System.Net;
using System.Net.Http.Json;
using System.Text;
using GoSit.Domain.Payments;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class PaymentTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Thursday, 15);
    private static readonly DateTime End = Start.AddHours(4);

    private sealed record IntentBody(
        Guid PaymentId, Guid CareBookingId, decimal Amount, string Currency,
        string Status, string ClientSecret);

    private sealed record ReceiptBody(
        Guid PaymentId, Guid CareBookingId, decimal Amount, decimal RefundedAmount,
        decimal NetAmount, string Currency, string Status, DateTime? SucceededAtUtc,
        IReadOnlyList<RefundBody> Refunds);

    private sealed record RefundBody(decimal Amount, string? Reason, DateTime CreatedAtUtc);

    private sealed record EarningsBody(
        string Currency, decimal TotalPaid, decimal PendingBalance,
        decimal LifetimeGross, decimal LifetimePlatformFees,
        IReadOnlyList<StatementLineBody> Statement);

    private sealed record StatementLineBody(
        Guid PayoutId, Guid CareBookingId, decimal Gross, decimal PlatformFee,
        decimal Net, decimal CommissionRate, string Status,
        DateTime CreatedAtUtc, DateTime? PaidAtUtc);

    private async Task<(HttpClient Requester, Guid CaregiverId, BookingBody Booking)> BookedAsync(
        decimal hourlyRate = 30m)
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: hourlyRate);
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);
        return (requester, caregiverId, booking);
    }

    private static Task<HttpResponseMessage> PostWebhookAsync(
        HttpClient client, string payload, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (signature is not null)
        {
            request.Headers.Add("Stripe-Signature", signature);
        }

        return client.SendAsync(request);
    }

    [Fact]
    public async Task A_requester_can_start_paying_for_their_booking()
    {
        var (requester, _, booking) = await BookedAsync();

        var response = await requester.PostAsync(
            $"/api/v1/bookings/{booking.Id}/payment-intent", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var intent = (await response.Content.ReadFromJsonAsync<IntentBody>())!;

        // 4 hours at 30/hour, computed from the booking rather than sent by the client.
        Assert.Equal(120m, intent.Amount);
        Assert.Equal("CAD", intent.Currency);
        Assert.False(string.IsNullOrWhiteSpace(intent.ClientSecret));
    }

    /// <summary>
    /// Retrying must reuse the provider's original intent, not create a second charge.
    /// </summary>
    [Fact]
    public async Task Starting_payment_twice_reuses_the_same_intent()
    {
        var (requester, _, booking) = await BookedAsync();

        var first = await requester.PostAsync($"/api/v1/bookings/{booking.Id}/payment-intent", null);
        var second = await requester.PostAsync($"/api/v1/bookings/{booking.Id}/payment-intent", null);

        var a = (await first.Content.ReadFromJsonAsync<IntentBody>())!;
        var b = (await second.Content.ReadFromJsonAsync<IntentBody>())!;

        Assert.Equal(a.PaymentId, b.PaymentId);
        Assert.Equal(a.ClientSecret, b.ClientSecret);
    }

    [Fact]
    public async Task Another_requester_cannot_start_paying_for_someone_elses_booking()
    {
        var (_, _, booking) = await BookedAsync();

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        var response = await stranger.PostAsync(
            $"/api/v1/bookings/{booking.Id}/payment-intent", null);

        // 404 rather than 403, so booking ids cannot be probed.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_start_a_payment()
    {
        var (_, caregiverId, booking) = await BookedAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var response = await caregiver.PostAsync(
            $"/api/v1/bookings/{booking.Id}/payment-intent", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Webhook security: the boundary the whole payment flow rests on ----

    /// <summary>
    /// Without a valid signature anyone who knew the URL could mark bookings paid.
    /// </summary>
    [Fact]
    public async Task An_unsigned_webhook_is_rejected_and_changes_nothing()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);

        var payload = StripeWebhookSigner.PaymentSucceeded("evt_unsigned", await IntentIdAsync(intent), 12000);

        var response = await PostWebhookAsync(factory.CreateClient(), payload, signature: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Processing, await ReadPaymentStatusAsync(booking.Id));
    }

    [Fact]
    public async Task A_webhook_signed_with_the_wrong_secret_is_rejected()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var payload = StripeWebhookSigner.PaymentSucceeded(
            "evt_wrong_secret", await IntentIdAsync(intent), 12000);

        var response = await PostWebhookAsync(
            factory.CreateClient(),
            payload,
            StripeWebhookSigner.Sign(payload, secret: "whsec_not_the_real_secret"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Processing, await ReadPaymentStatusAsync(booking.Id));
    }

    /// <summary>
    /// The signature covers the body, so altering the amount after signing must fail.
    /// </summary>
    [Fact]
    public async Task A_tampered_payload_is_rejected()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var intentId = await IntentIdAsync(intent);

        var payload = StripeWebhookSigner.PaymentSucceeded("evt_tampered", intentId, 12000);
        var signature = StripeWebhookSigner.Sign(payload);

        var tampered = payload.Replace("12000", "1");

        var response = await PostWebhookAsync(factory.CreateClient(), tampered, signature);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Processing, await ReadPaymentStatusAsync(booking.Id));
    }

    /// <summary>
    /// A captured payload replayed later must not still work.
    /// </summary>
    [Fact]
    public async Task A_webhook_with_a_stale_timestamp_is_rejected()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var payload = StripeWebhookSigner.PaymentSucceeded(
            "evt_stale", await IntentIdAsync(intent), 12000);

        // Well outside the 300-second tolerance.
        var signature = StripeWebhookSigner.Sign(payload, DateTime.UtcNow.AddHours(-1));

        var response = await PostWebhookAsync(factory.CreateClient(), payload, signature);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Processing, await ReadPaymentStatusAsync(booking.Id));
    }

    // ---- Webhook behaviour ----

    [Fact]
    public async Task A_valid_success_webhook_settles_the_payment()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var payload = StripeWebhookSigner.PaymentSucceeded(
            $"evt_{Guid.NewGuid():N}", await IntentIdAsync(intent), 12000);

        var response = await PostWebhookAsync(
            factory.CreateClient(), payload, StripeWebhookSigner.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.Succeeded, await ReadPaymentStatusAsync(booking.Id));
    }

    /// <summary>Webhooks are delivered at least once; a repeat must be a no-op.</summary>
    [Fact]
    public async Task The_same_webhook_delivered_twice_is_processed_once()
    {
        var (requester, caregiverId, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = StripeWebhookSigner.PaymentSucceeded(eventId, await IntentIdAsync(intent), 12000);
        var signature = StripeWebhookSigner.Sign(payload);

        var first = await PostWebhookAsync(factory.CreateClient(), payload, signature);
        var second = await PostWebhookAsync(factory.CreateClient(), payload, signature);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // One payout, not two — the caregiver must not be owed twice.
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var earnings = await caregiver.GetFromJsonAsync<EarningsBody>("/api/v1/caregivers/me/earnings");
        Assert.Single(earnings!.Statement);
    }

    /// <summary>
    /// If the provider reports collecting a different amount, the two sides disagree
    /// about money. That must not be silently accepted.
    /// </summary>
    [Fact]
    public async Task A_success_webhook_for_the_wrong_amount_does_not_settle_the_payment()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var payload = StripeWebhookSigner.PaymentSucceeded(
            $"evt_{Guid.NewGuid():N}", await IntentIdAsync(intent), 1);

        var response = await PostWebhookAsync(
            factory.CreateClient(), payload, StripeWebhookSigner.Sign(payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Processing, await ReadPaymentStatusAsync(booking.Id));
    }

    [Fact]
    public async Task A_failure_webhook_records_the_failure_code()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var payload = StripeWebhookSigner.PaymentFailed(
            $"evt_{Guid.NewGuid():N}", await IntentIdAsync(intent), "card_declined");

        var response = await PostWebhookAsync(
            factory.CreateClient(), payload, StripeWebhookSigner.Sign(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.Failed, await ReadPaymentStatusAsync(booking.Id));
    }

    [Fact]
    public async Task An_unrecognized_event_type_is_acknowledged_and_ignored()
    {
        var payload = $$"""
        {
          "id": "evt_{{Guid.NewGuid():N}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "customer.created",
          "data": { "object": { "id": "cus_test", "object": "customer" } }
        }
        """;

        var response = await PostWebhookAsync(
            factory.CreateClient(), payload, StripeWebhookSigner.Sign(payload));

        // Acknowledged so the provider stops retrying something GoSit does not act on.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Refunds, receipts and earnings ----

    [Fact]
    public async Task A_refund_webhook_reduces_the_net_amount()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        var intentId = await IntentIdAsync(intent);

        await SettleAsync(intentId, 12000);

        var refund = StripeWebhookSigner.ChargeRefunded(
            $"evt_{Guid.NewGuid():N}", intentId, 2000, $"re_{Guid.NewGuid():N}");

        await PostWebhookAsync(factory.CreateClient(), refund, StripeWebhookSigner.Sign(refund));

        var receipt = await requester.GetFromJsonAsync<ReceiptBody>(
            $"/api/v1/bookings/{booking.Id}/payment");

        Assert.Equal("PartiallyRefunded", receipt!.Status);
        Assert.Equal(20m, receipt.RefundedAmount);
        Assert.Equal(100m, receipt.NetAmount);
    }

    [Fact]
    public async Task Both_participants_can_read_the_receipt_but_nobody_else_can()
    {
        var (requester, caregiverId, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        await SettleAsync(await IntentIdAsync(intent), 12000);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await requester.GetAsync($"/api/v1/bookings/{booking.Id}/payment")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await caregiver.GetAsync($"/api/v1/bookings/{booking.Id}/payment")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/v1/bookings/{booking.Id}/payment")).StatusCode);
    }

    /// <summary>
    /// GoSit never sees a card number, and the provider's client secret has no place in
    /// a receipt.
    /// </summary>
    [Fact]
    public async Task A_receipt_carries_no_card_or_provider_secret()
    {
        var (requester, _, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        await SettleAsync(await IntentIdAsync(intent), 12000);

        var body = await requester.GetStringAsync($"/api/v1/bookings/{booking.Id}/payment");

        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("card", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pi_", body);
    }

    [Fact]
    public async Task A_settled_payment_credits_the_caregivers_wallet_minus_the_fee()
    {
        var (requester, caregiverId, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        await SettleAsync(await IntentIdAsync(intent), 12000);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var earnings = await caregiver.GetFromJsonAsync<EarningsBody>("/api/v1/caregivers/me/earnings");

        var line = Assert.Single(earnings!.Statement);
        Assert.Equal(120m, line.Gross);
        Assert.Equal(18m, line.PlatformFee);
        Assert.Equal(102m, line.Net);
        // The split always adds back to the gross.
        Assert.Equal(line.Gross, line.PlatformFee + line.Net);

        // Earned but not yet transferred.
        Assert.Equal(102m, earnings.PendingBalance);
        Assert.Equal(0m, earnings.TotalPaid);
    }

    /// <summary>
    /// REDUCED_COMMISSION, through the path that actually creates a payout.
    ///
    /// <c>PlatformFeePolicy</c> is unit tested and correct, which is exactly what makes
    /// this worth asserting separately: the policy could be perfect while the lookup
    /// feeding it answers false for everyone — the wrong account resolved, the requester
    /// entitled instead of the caregiver, the feature code mistyped. None of that fails
    /// a build, and the only symptom would be caregivers quietly paying full commission
    /// on a plan they bought to avoid it.
    /// </summary>
    [Fact]
    public async Task A_subscribed_caregiver_keeps_more_of_the_same_gross()
    {
        var (requester, caregiverId, booking) = await BookedAsync();

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var subscribed = await caregiver.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode = "PRO_CAREGIVER", promoCode = (string?)null });

        subscribed.EnsureSuccessStatusCode();

        var intent = await StartPaymentAsync(requester, booking.Id);
        await SettleAsync(await IntentIdAsync(intent), 12000);

        var earnings = await caregiver.GetFromJsonAsync<EarningsBody>(
            "/api/v1/caregivers/me/earnings");

        var line = Assert.Single(earnings!.Statement);

        // The same $120 of work as the standard-rate test above, at 10% rather than 15%.
        Assert.Equal(120m, line.Gross);
        Assert.Equal(12m, line.PlatformFee);
        Assert.Equal(108m, line.Net);
        Assert.Equal(0.10m, line.CommissionRate);

        // Still no cent created or lost.
        Assert.Equal(line.Gross, line.PlatformFee + line.Net);
    }

    /// <summary>
    /// The rate is stamped when the payout is created, so cancelling a plan does not
    /// claw back what was already earned — and the reverse, which is the one that would
    /// cost money: subscribing tomorrow must not reprice yesterday's shift.
    /// </summary>
    [Fact]
    public async Task Subscribing_after_the_work_does_not_reprice_it()
    {
        var (requester, caregiverId, booking) = await BookedAsync();

        var intent = await StartPaymentAsync(requester, booking.Id);
        await SettleAsync(await IntentIdAsync(intent), 12000);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        (await caregiver.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode = "PRO_CAREGIVER", promoCode = (string?)null }))
            .EnsureSuccessStatusCode();

        var earnings = await caregiver.GetFromJsonAsync<EarningsBody>(
            "/api/v1/caregivers/me/earnings");

        var line = Assert.Single(earnings!.Statement);

        Assert.Equal(0.15m, line.CommissionRate);
        Assert.Equal(102m, line.Net);
    }

    [Fact]
    public async Task A_caregiver_only_sees_their_own_earnings()
    {
        var (requester, caregiverId, booking) = await BookedAsync();
        var intent = await StartPaymentAsync(requester, booking.Id);
        await SettleAsync(await IntentIdAsync(intent), 12000);

        var otherCaregiverId = await factory.CreateBookableCaregiverAsync();
        var other = await factory.CaregiverClientForAsync(otherCaregiverId);

        var earnings = await other.GetFromJsonAsync<EarningsBody>("/api/v1/caregivers/me/earnings");

        Assert.Empty(earnings!.Statement);
        Assert.Equal(0m, earnings.PendingBalance);
    }

    [Fact]
    public async Task A_requester_cannot_read_caregiver_earnings()
    {
        var (requester, _, _) = await BookedAsync();

        var response = await requester.GetAsync("/api/v1/caregivers/me/earnings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Payment_endpoints_require_authentication()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/v1/bookings/{Guid.NewGuid()}/payment")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/caregivers/me/earnings")).StatusCode);
    }

    // ---- helpers ----

    private static async Task<IntentBody> StartPaymentAsync(HttpClient requester, Guid bookingId)
    {
        var response = await requester.PostAsync($"/api/v1/bookings/{bookingId}/payment-intent", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntentBody>())!;
    }

    private async Task SettleAsync(string intentId, long amountCents)
    {
        var payload = StripeWebhookSigner.PaymentSucceeded(
            $"evt_{Guid.NewGuid():N}", intentId, amountCents);

        var response = await PostWebhookAsync(
            factory.CreateClient(), payload, StripeWebhookSigner.Sign(payload));

        response.EnsureSuccessStatusCode();
    }

    /// <summary>The provider intent id is not returned to clients, so it is read from storage.</summary>
    private async Task<string> IntentIdAsync(IntentBody intent)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return (await context.Payments
            .Where(p => p.Id == intent.PaymentId)
            .Select(p => p.ProviderPaymentIntentId)
            .SingleAsync())!;
    }

    private async Task<PaymentStatus> ReadPaymentStatusAsync(Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Payments
            .Where(p => p.CareBookingId == bookingId)
            .Select(p => p.Status)
            .SingleAsync();
    }
}
