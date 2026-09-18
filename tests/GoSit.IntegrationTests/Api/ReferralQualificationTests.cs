using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// The half of the referral programme that had no caller.
///
/// Recording a referral and qualifying one were both implemented, but nothing connected
/// the second to a booking, so every invitation stayed <c>Pending</c> forever. The
/// referrals screen meanwhile told people "when someone joins with your code and
/// completes their first booking, the referral counts towards you" — a promise the
/// backend never kept. These pin the promise to the behaviour.
/// </summary>
[Collection(nameof(ApiCollection))]
public class ReferralQualificationTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);
    private static readonly DateTime End = Start.AddHours(4);

    private sealed record ReferralBody(
        Guid Id, string Status, DateTime CreatedAtUtc, DateTime? QualifiedAtUtc, decimal? RewardAmount);

    private sealed record ReferralSummaryBody(
        string MyReferralCode, int PendingCount, int QualifiedCount,
        int RewardedCount, IReadOnlyList<ReferralBody> Referrals);

    private static async Task<string> CodeForAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<ReferralSummaryBody>("/api/v1/me/referrals"))!.MyReferralCode;

    private static Task<ReferralSummaryBody?> SummaryForAsync(HttpClient client) =>
        client.GetFromJsonAsync<ReferralSummaryBody>("/api/v1/me/referrals");

    /// <summary>A requester who was invited by <paramref name="referrer"/>.</summary>
    private async Task<(HttpClient Client, Guid RecipientId)> ReferredRequesterAsync(
        HttpClient referrer)
    {
        var client = await factory.RegisterCareRequesterAsync();

        var applied = await client.PostAsJsonAsync(
            "/api/v1/me/referrals", new { code = await CodeForAsync(referrer) });

        applied.EnsureSuccessStatusCode();

        await client.CreateCareRequesterProfileAsync();
        var recipientId = await client.AddCareRecipientAsync();

        return (client, recipientId);
    }

    [Fact]
    public async Task A_referral_qualifies_when_the_referee_completes_a_booking()
    {
        var referrer = await factory.RegisterCareRequesterAsync();
        var (referee, recipientId) = await ReferredRequesterAsync(referrer);

        Assert.Equal(1, (await SummaryForAsync(referrer))!.PendingCount);

        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);
        var booking = await referee.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null);

        var summary = (await SummaryForAsync(referrer))!;

        Assert.Equal(0, summary.PendingCount);
        Assert.Equal(1, summary.QualifiedCount);
        Assert.NotNull(Assert.Single(summary.Referrals).QualifiedAtUtc);
    }

    /// <summary>
    /// Referrals must not depend on a caregiver remembering to check out. A booking the
    /// platform closed is still a completed booking, and treating the two differently
    /// would cost a family their referral because of someone else's forgetfulness.
    /// </summary>
    [Fact]
    public async Task A_referral_qualifies_when_the_sweep_completes_the_booking()
    {
        var referrer = await factory.RegisterCareRequesterAsync();
        var (referee, recipientId) = await ReferredRequesterAsync(referrer);

        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);
        var booking = await referee.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);

        // Abandoned: past the point where the caregiver's own check-out is refused.
        var backdate = TimeSpan.FromHours(-15);
        await factory.ShiftScheduleAsync(booking.Id, backdate);
        await factory.ShiftCheckInAsync(booking.Id, backdate);

        await factory.RunBookingSweepAsync();

        Assert.Equal(1, (await SummaryForAsync(referrer))!.QualifiedCount);
    }

    /// <summary>
    /// A referred caregiver qualifies on their own work, not only a referred family on
    /// theirs — otherwise inviting caregivers, which the caregiver screen explicitly
    /// asks people to do, would never pay out.
    /// </summary>
    [Fact]
    public async Task A_referred_caregiver_qualifies_on_the_work_they_complete()
    {
        var referrer = await factory.RegisterCareRequesterAsync();

        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var applied = await caregiver.PostAsJsonAsync(
            "/api/v1/me/referrals", new { code = await CodeForAsync(referrer) });

        applied.EnsureSuccessStatusCode();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null);

        Assert.Equal(1, (await SummaryForAsync(referrer))!.QualifiedCount);
    }

    /// <summary>
    /// Qualification happens once. A second completed booking must not turn one
    /// invitation into two, and the qualification timestamp must not drift forward.
    /// </summary>
    [Fact]
    public async Task A_second_completed_booking_does_not_qualify_the_referral_again()
    {
        var referrer = await factory.RegisterCareRequesterAsync();
        var (referee, recipientId) = await ReferredRequesterAsync(referrer);

        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var first = await CompleteBookingAsync(referee, caregiver, caregiverId, recipientId, Start);
        var qualifiedAt = Assert.Single((await SummaryForAsync(referrer))!.Referrals).QualifiedAtUtc;

        Assert.NotNull(qualifiedAt);
        Assert.NotEqual(Guid.Empty, first);

        await CompleteBookingAsync(
            referee, caregiver, caregiverId, recipientId, Start.AddDays(7));

        var summary = (await SummaryForAsync(referrer))!;

        Assert.Equal(1, summary.QualifiedCount);
        Assert.Equal(qualifiedAt, Assert.Single(summary.Referrals).QualifiedAtUtc);
    }

    /// <summary>
    /// A booking nobody completed leaves the referral where it was. Cancelling is the
    /// obvious way to farm a referral programme if completion is not what counts.
    /// </summary>
    [Fact]
    public async Task A_cancelled_booking_leaves_the_referral_pending()
    {
        var referrer = await factory.RegisterCareRequesterAsync();
        var (referee, recipientId) = await ReferredRequesterAsync(referrer);

        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var booking = await referee.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var cancelled = await referee.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/cancel", new { reason = "Plans changed." });

        cancelled.EnsureSuccessStatusCode();

        var summary = (await SummaryForAsync(referrer))!;

        Assert.Equal(1, summary.PendingCount);
        Assert.Equal(0, summary.QualifiedCount);
    }

    /// <summary>
    /// Someone with no referral against them completes bookings constantly. The
    /// completion path must not fail, or every check-out breaks for most of the market.
    /// </summary>
    [Fact]
    public async Task A_booking_by_an_unreferred_account_completes_normally()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var caregiverId = await factory.CreateBookableCaregiverAsync(hourlyRate: 30m);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var bookingId = await CompleteBookingAsync(
            requester, caregiver, caregiverId, recipientId, Start);

        Assert.Equal(
            Domain.Bookings.BookingStatus.Completed,
            await factory.ReadStatusAsync(bookingId));
    }

    private async Task<Guid> CompleteBookingAsync(
        HttpClient requester,
        HttpClient caregiver,
        Guid caregiverProfileId,
        Guid recipientId,
        DateTime start)
    {
        var booking = await requester.CreateBookingAsync(
            caregiverProfileId, recipientId, start, start.AddHours(4));

        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null);
        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - start);
        await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null);

        var checkedOut = await caregiver.PostAsync(
            $"/api/v1/bookings/{booking.Id}/check-out", null);

        checkedOut.EnsureSuccessStatusCode();

        return booking.Id;
    }
}
