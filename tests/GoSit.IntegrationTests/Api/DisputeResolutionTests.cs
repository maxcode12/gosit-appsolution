using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Payouts;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Staff settling a disputed booking.
///
/// A dispute holds the caregiver's payout, which is what makes the dispute window mean
/// anything — before that, the money left within fifteen minutes of the payment settling.
/// But a hold with no release is money stranded indefinitely, and a disputed booking was
/// invisible to staff: not in the incident queue, not anywhere.
///
/// The decision is one number — what goes back to the family — and everything else
/// follows from it. These pin what each answer does to the money, because that is the
/// part nobody can check by reading a screen.
/// </summary>
[Collection(nameof(ApiCollection))]
public class DisputeResolutionTests(GoSitApiFactory factory)
{
    private sealed record OpenDisputeBody(
        Guid CareBookingId,
        string CareRequesterName,
        string CaregiverName,
        decimal AmountPaid,
        decimal PayoutAmount,
        bool PayoutIsRecoverable);

    private sealed record EarningsBody(decimal PendingBalance);

    /// <summary>
    /// A completed, paid-for booking that the family has disputed.
    ///
    /// The amount is returned rather than assumed: check-out bills the hours actually
    /// worked, which in a test is a matter of minutes, so hard-coding the four scheduled
    /// hours would be asserting a number the product never produces.
    /// </summary>
    private async Task<(Guid BookingId, decimal Paid, Guid PayoutId)> DisputedBookingAsync()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);

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

        await SettleAsync(booking.Id);

        (await requester.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/dispute",
            new { reason = "The caregiver left two hours early." }))
            .EnsureSuccessStatusCode();

        var payoutId = await PayoutIdAsync(booking.Id);

        return (booking.Id, (await ReadPayoutAsync(payoutId)).GrossAmount, payoutId);
    }

    /// <summary>Settles the payment the only way it can be: a signed webhook.</summary>
    private async Task SettleAsync(Guid bookingId)
    {
        string intentId;
        long amountCents;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

            var payment = await context.Payments
                .AsNoTracking()
                .SingleAsync(p => p.CareBookingId == bookingId);

            intentId = payment.ProviderPaymentIntentId!;
            amountCents = (long)(payment.Amount * 100);
        }

        var payload = StripeWebhookSigner.PaymentSucceeded(
            $"evt_{Guid.NewGuid():N}", intentId, amountCents);

        var content = new StringContent(
            payload, System.Text.Encoding.UTF8, "application/json");

        content.Headers.Add("Stripe-Signature", StripeWebhookSigner.Sign(payload));

        (await factory.CreateClient().PostAsync("/api/v1/webhooks/stripe", content))
            .EnsureSuccessStatusCode();
    }

    private async Task<Guid> PayoutIdAsync(Guid bookingId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Payouts
            .AsNoTracking()
            .Where(p => p.CareBookingId == bookingId)
            .Select(p => p.Id)
            .SingleAsync();
    }

    private async Task<Payout> ReadPayoutAsync(Guid payoutId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        return await context.Payouts.AsNoTracking().SingleAsync(p => p.Id == payoutId);
    }

    private static Task<HttpResponseMessage> ResolveAsync(
        HttpClient admin, Guid bookingId, decimal refundAmount, string notes = "Settled.") =>
        admin.PostAsJsonAsync(
            $"/api/v1/admin/disputes/{bookingId}/resolve",
            new { refundAmount, notes });

    // ---- the queue ----

    /// <summary>
    /// The gap this closes. A disputed booking used to appear on no staff screen at all,
    /// while holding money nobody could release.
    /// </summary>
    [Fact]
    public async Task A_disputed_booking_appears_in_the_staff_queue()
    {
        var (bookingId, paid, _) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        var queue = await admin.GetFromJsonAsync<List<OpenDisputeBody>>(
            "/api/v1/admin/disputes?take=200");

        var row = Assert.Single(queue!, d => d.CareBookingId == bookingId);

        // Both sides are named, because a decision about money needs to know whose.
        Assert.False(string.IsNullOrWhiteSpace(row.CareRequesterName));
        Assert.False(string.IsNullOrWhiteSpace(row.CaregiverName));

        Assert.Equal(paid, row.AmountPaid);
        Assert.True(row.PayoutIsRecoverable);
    }

    [Fact]
    public async Task The_dispute_queue_is_staff_only()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.GetAsync("/api/v1/admin/disputes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- what the decision does to the money ----

    /// <summary>
    /// A dismissed dispute pays the caregiver in full. Zero is a real answer, not a
    /// missing one.
    /// </summary>
    [Fact]
    public async Task Refunding_nothing_releases_the_payout_in_full()
    {
        var (bookingId, paid, payoutId) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        (await ResolveAsync(admin, bookingId, 0m, "The hours were worked as booked."))
            .EnsureSuccessStatusCode();

        var payout = await ReadPayoutAsync(payoutId);

        Assert.False(payout.IsHeldForDispute);
        Assert.Equal(PayoutStatus.Queued, payout.Status);

        // Untouched: the whole booking, and a split that still adds back to it.
        //
        // Deliberately not recomputing the fee here — an assertion that reimplements the
        // rounding rule tests the copy rather than the original, and disagrees with it by
        // a cent the first time either changes.
        Assert.Equal(paid, payout.GrossAmount);
        Assert.Equal(payout.GrossAmount, payout.PlatformFee + payout.Amount);
    }

    [Fact]
    public async Task Refunding_everything_cancels_the_payout()
    {
        var (bookingId, paid, payoutId) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        (await ResolveAsync(admin, bookingId, paid, "The caregiver did not attend."))
            .EnsureSuccessStatusCode();

        var payout = await ReadPayoutAsync(payoutId);

        Assert.Equal(PayoutStatus.Cancelled, payout.Status);
        Assert.False(payout.IsHeldForDispute);
    }

    /// <summary>
    /// A partial refund recomputes the split on what the family kept paying for —
    /// including GoSit's cut, which must not be taken on money that was handed back.
    /// </summary>
    [Fact]
    public async Task A_partial_refund_reduces_the_payout_and_the_platform_fee()
    {
        var (bookingId, paid, payoutId) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        var refund = Math.Round(paid / 2, 2);
        var remaining = paid - refund;

        (await ResolveAsync(admin, bookingId, refund, "Half the time was not worked."))
            .EnsureSuccessStatusCode();

        var payout = await ReadPayoutAsync(payoutId);

        Assert.Equal(remaining, payout.GrossAmount);

        // Recomputed on what is left, so GoSit does not keep its cut of money that was
        // handed back.
        Assert.Equal(Math.Round(remaining * 0.15m, 2), payout.PlatformFee);

        // And the split still adds back to the reduced gross.
        Assert.Equal(payout.GrossAmount, payout.PlatformFee + payout.Amount);

        Assert.False(payout.IsHeldForDispute);
    }

    [Fact]
    public async Task A_refund_larger_than_the_family_paid_is_refused()
    {
        var (bookingId, _, _) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        var response = await ResolveAsync(admin, bookingId, 5_000m);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Settling twice must not refund twice. The second attempt is refused by the
    /// aggregate before it reaches the provider.
    /// </summary>
    [Fact]
    public async Task A_dispute_cannot_be_settled_twice()
    {
        var (bookingId, _, _) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        (await ResolveAsync(admin, bookingId, 0m)).EnsureSuccessStatusCode();

        var second = await ResolveAsync(admin, bookingId, 0m);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    /// <summary>Both parties are told why, so the note is required rather than optional.</summary>
    [Fact]
    public async Task Settling_without_a_note_is_refused()
    {
        var (bookingId, _, _) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        var response = await ResolveAsync(admin, bookingId, 0m, notes: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Once settled the booking leaves the queue, or staff would keep meeting decisions
    /// they have already made.
    /// </summary>
    [Fact]
    public async Task A_settled_dispute_leaves_the_queue()
    {
        var (bookingId, _, _) = await DisputedBookingAsync();
        var admin = await factory.RegisterAdminAsync();

        (await ResolveAsync(admin, bookingId, 0m)).EnsureSuccessStatusCode();

        var queue = await admin.GetFromJsonAsync<List<OpenDisputeBody>>(
            "/api/v1/admin/disputes?take=200");

        Assert.DoesNotContain(queue!, d => d.CareBookingId == bookingId);
    }

    /// <summary>
    /// The caregiver's own screen reflects the outcome. A cancelled payout must not keep
    /// showing as money on its way.
    /// </summary>
    [Fact]
    public async Task A_full_refund_removes_the_money_from_the_caregivers_balance()
    {
        var (bookingId, paid, payoutId) = await DisputedBookingAsync();

        var caregiverProfileId = (await ReadPayoutAsync(payoutId)).CaregiverProfileId;
        var caregiver = await factory.CaregiverClientForAsync(caregiverProfileId);

        var before = await caregiver.GetFromJsonAsync<EarningsBody>(
            "/api/v1/caregivers/me/earnings");

        // Whatever the payout is worth, that is what the balance shows.
        Assert.Equal((await ReadPayoutAsync(payoutId)).Amount, before!.PendingBalance);

        var admin = await factory.RegisterAdminAsync();
        (await ResolveAsync(admin, bookingId, paid)).EnsureSuccessStatusCode();

        var after = await caregiver.GetFromJsonAsync<EarningsBody>(
            "/api/v1/caregivers/me/earnings");

        Assert.Equal(0m, after!.PendingBalance);
    }

    // ---- telling the two people waiting ----

    private sealed record NotificationBody(string Type, string? Body);

    private sealed record NotificationListBody(
        IReadOnlyList<NotificationBody> Notifications, int UnreadCount);

    private sealed record BookingBodyWithOutcome(
        DateTime? DisputeResolvedAtUtc, string? DisputeResolutionNotes);

    private static async Task<IReadOnlyList<NotificationBody>> NotificationsAsync(
        HttpClient client)
    {
        var list = await client.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");
        return list!.Notifications;
    }

    /// <summary>
    /// The note the admin form makes mandatory has to arrive somewhere.
    ///
    /// It was recorded and read by nobody: no notification raised it, and no response
    /// carried it. The form told the staff member writing it that both people would read
    /// it, which made the requirement a lie to them as well as a silence to the two people
    /// waiting on an answer.
    /// </summary>
    [Fact]
    public async Task Settling_a_dispute_tells_both_sides_and_shows_them_the_same_words()
    {
        var (bookingId, _, payoutId) = await DisputedBookingAsync();

        var caregiverProfileId = (await ReadPayoutAsync(payoutId)).CaregiverProfileId;
        var caregiver = await factory.CaregiverClientForAsync(caregiverProfileId);

        const string notes =
            "Check-in and check-out show three hours, not the five booked. "
            + "Refunding the difference.";

        var admin = await factory.RegisterAdminAsync();
        (await ResolveAsync(admin, bookingId, 0m, notes)).EnsureSuccessStatusCode();

        Assert.Contains(await NotificationsAsync(caregiver), n => n.Type == "DisputeResolved");

        // And the decision itself is readable, identically, by the caregiver.
        var seen = await caregiver.GetFromJsonAsync<BookingBodyWithOutcome>(
            $"/api/v1/bookings/{bookingId}");

        Assert.Equal(notes, seen!.DisputeResolutionNotes);
        Assert.NotNull(seen.DisputeResolvedAtUtc);
    }

    /// <summary>
    /// The notification travels by email and lands on lock screens. What a staff member
    /// wrote about what happened in a family's home stays on the booking.
    /// </summary>
    [Fact]
    public async Task The_settlement_notification_does_not_carry_the_staff_note()
    {
        var (bookingId, _, payoutId) = await DisputedBookingAsync();

        var caregiverProfileId = (await ReadPayoutAsync(payoutId)).CaregiverProfileId;
        var caregiver = await factory.CaregiverClientForAsync(caregiverProfileId);

        var admin = await factory.RegisterAdminAsync();

        (await ResolveAsync(admin, bookingId, 0m, "The child was left alone for an hour."))
            .EnsureSuccessStatusCode();

        var settled = Assert.Single(
            await NotificationsAsync(caregiver), n => n.Type == "DisputeResolved");

        Assert.DoesNotContain(
            "left alone", settled.Body ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
