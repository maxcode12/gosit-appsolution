using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// The screen behind CAREGIVER_INSIGHTS — the one feature code that had nothing to gate.
///
/// Two things matter more than the arithmetic. A rate must be unknown rather than zero
/// when there is nothing to divide by, and a request the platform closed unanswered must
/// never be counted as a decline: telling a caregiver they turned down work they were
/// never shown would be worse than showing them nothing.
/// </summary>
[Collection(nameof(ApiCollection))]
public class CaregiverInsightsTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);

    private sealed record InsightsBody(
        int WindowDays,
        int RequestsReceived,
        int Accepted,
        int Declined,
        int LapsedUnanswered,
        int AwaitingYourReply,
        int CancelledByYou,
        double? AcceptanceRate,
        double? ResponseRate,
        int? MedianMinutesToAccept,
        int CompletedInWindow,
        int CompletedLifetime,
        int RepeatFamilies,
        decimal? AverageRating,
        int ReviewCount,
        bool SearchPerformanceAvailable);

    private static async Task SubscribeAsync(HttpClient client, string planCode)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode, promoCode = (string?)null });

        response.EnsureSuccessStatusCode();
    }

    /// <summary>A caregiver with a profile and the Pro plan.</summary>
    private async Task<(Guid ProfileId, HttpClient Client)> SubscribedCaregiverAsync()
    {
        var profileId = await factory.CreateBookableCaregiverAsync();
        var client = await factory.CaregiverClientForAsync(profileId);

        await SubscribeAsync(client, "PRO_CAREGIVER");

        return (profileId, client);
    }

    private static Task<InsightsBody?> InsightsAsync(HttpClient caregiver) =>
        caregiver.GetFromJsonAsync<InsightsBody>("/api/v1/caregivers/me/insights");

    // ---- the gate ----

    [Fact]
    public async Task Insights_are_refused_on_the_free_plan()
    {
        var profileId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(profileId);

        var response = await caregiver.GetAsync("/api/v1/caregivers/me/insights");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A care requester is refused by role before the plan is ever considered — buying a
    /// requester plan must not open a caregiver's screen.
    /// </summary>
    [Fact]
    public async Task A_care_requester_cannot_read_caregiver_insights()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await SubscribeAsync(requester, "PLUS_REQUESTER");

        var response = await requester.GetAsync("/api/v1/caregivers/me/insights");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Insights_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/caregivers/me/insights");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- unknown is not zero ----

    /// <summary>
    /// A caregiver nobody has asked for accepts no percent of nothing. Reporting 0%
    /// would read as a criticism of something that never happened.
    /// </summary>
    [Fact]
    public async Task A_caregiver_with_no_requests_has_unknown_rates_not_zero()
    {
        var (_, caregiver) = await SubscribedCaregiverAsync();

        var insights = await InsightsAsync(caregiver);

        Assert.Equal(0, insights!.RequestsReceived);
        Assert.Null(insights.AcceptanceRate);
        Assert.Null(insights.ResponseRate);
        Assert.Null(insights.MedianMinutesToAccept);
        Assert.Equal(0, insights.CompletedLifetime);
        Assert.Null(insights.AverageRating);
    }

    /// <summary>
    /// The promise the plan makes is "search and bookings", and both halves are measured
    /// now. This asserted the opposite while only bookings were counted — the flag was
    /// what let the screen say "not measured" instead of showing a zero that would have
    /// read as "nobody is finding you".
    ///
    /// The counting itself is covered by <see cref="CaregiverExposureTests"/>.
    /// </summary>
    [Fact]
    public async Task Search_performance_is_reported_as_available()
    {
        var (_, caregiver) = await SubscribedCaregiverAsync();

        var insights = await InsightsAsync(caregiver);

        Assert.True(insights!.SearchPerformanceAvailable);
    }

    // ---- the arithmetic ----

    [Fact]
    public async Task Accepting_and_declining_are_counted_separately()
    {
        var (profileId, caregiver) = await SubscribedCaregiverAsync();

        var toAccept = await RequestAsync(profileId, Start);
        var toDecline = await RequestAsync(profileId, Start.AddDays(1));

        (await caregiver.PostAsync($"/api/v1/bookings/{toAccept}/accept", null))
            .EnsureSuccessStatusCode();

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{toDecline}/decline", new { reason = "Already booked." }))
            .EnsureSuccessStatusCode();

        var insights = await InsightsAsync(caregiver);

        Assert.Equal(2, insights!.RequestsReceived);
        Assert.Equal(1, insights.Accepted);
        Assert.Equal(1, insights.Declined);
        Assert.Equal(0, insights.LapsedUnanswered);

        // Both answered, half accepted.
        Assert.Equal(1.0, insights.ResponseRate);
        Assert.Equal(0.5, insights.AcceptanceRate);
    }

    /// <summary>
    /// A request still sitting in the queue is work to do, not a reply missed.
    ///
    /// The screen reads the response rate beside a sentence explaining it, and with only
    /// the lapsed count to go on that sentence said "you answered every request" next to
    /// a rate of 50% — two figures from the same data contradicting each other on the
    /// same row.
    /// </summary>
    [Fact]
    public async Task An_unanswered_request_is_reported_as_still_waiting()
    {
        var (profileId, caregiver) = await SubscribedCaregiverAsync();

        var answered = await RequestAsync(profileId, Start);
        await RequestAsync(profileId, Start.AddDays(1));

        (await caregiver.PostAsync($"/api/v1/bookings/{answered}/accept", null))
            .EnsureSuccessStatusCode();

        var insights = await InsightsAsync(caregiver);

        Assert.Equal(2, insights!.RequestsReceived);
        Assert.Equal(0.5, insights.ResponseRate);

        // The half that is not a miss.
        Assert.Equal(1, insights.AwaitingYourReply);
        Assert.Equal(0, insights.LapsedUnanswered);
    }

    /// <summary>
    /// The distinction the whole outcome enum exists for. The platform closes a request
    /// nobody answered; counting that as a decline would blame the caregiver for it.
    /// </summary>
    [Fact]
    public async Task A_request_closed_unanswered_is_not_counted_as_a_decline()
    {
        var (profileId, caregiver) = await SubscribedCaregiverAsync();

        var ignored = await RequestAsync(profileId, Start);

        // An hour past its start, comfortably beyond the grace the sweep allows.
        await factory.ShiftScheduleAsync(ignored, DateTime.UtcNow.AddHours(-1) - Start);
        await factory.RunBookingSweepAsync();

        var insights = await InsightsAsync(caregiver);

        Assert.Equal(1, insights!.RequestsReceived);
        Assert.Equal(0, insights.Declined);
        Assert.Equal(1, insights.LapsedUnanswered);
        Assert.Equal(0, insights.AwaitingYourReply);

        // Nothing was answered, so there is no acceptance rate to report — but the
        // response rate is a real zero: one request arrived and none was answered.
        Assert.Equal(0.0, insights.ResponseRate);
        Assert.Null(insights.AcceptanceRate);
    }

    /// <summary>
    /// A booking that was worked was also accepted. Counting only rows still sitting in
    /// Accepted would tell a caregiver who accepted and completed everything that they
    /// never replied.
    /// </summary>
    [Fact]
    public async Task Completed_work_still_counts_as_having_been_accepted()
    {
        var (profileId, caregiver) = await SubscribedCaregiverAsync();

        var bookingId = await RequestAsync(profileId, Start);

        (await caregiver.PostAsync($"/api/v1/bookings/{bookingId}/accept", null))
            .EnsureSuccessStatusCode();

        await factory.ShiftScheduleAsync(bookingId, DateTime.UtcNow - Start);
        (await caregiver.PostAsync($"/api/v1/bookings/{bookingId}/check-in", null))
            .EnsureSuccessStatusCode();
        (await caregiver.PostAsync($"/api/v1/bookings/{bookingId}/check-out", null))
            .EnsureSuccessStatusCode();

        var insights = await InsightsAsync(caregiver);

        Assert.Equal(1, insights!.Accepted);
        Assert.Equal(1.0, insights.ResponseRate);
        Assert.Equal(1.0, insights.AcceptanceRate);
        Assert.Equal(1, insights.CompletedInWindow);
        Assert.Equal(1, insights.CompletedLifetime);
    }

    /// <summary>
    /// A family returning is the signal this screen exists for. Counted as accounts —
    /// the response says how many, never who.
    /// </summary>
    [Fact]
    public async Task A_family_who_books_twice_counts_once_as_a_repeat()
    {
        var (profileId, caregiver) = await SubscribedCaregiverAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        await CompleteAsync(requester, caregiver, profileId, recipientId, Start);
        await CompleteAsync(requester, caregiver, profileId, recipientId, Start.AddDays(7));

        // A different family, once only — must not count.
        var oneOff = await factory.RegisterCareRequesterAsync();
        await oneOff.CreateCareRequesterProfileAsync();
        var otherRecipient = await oneOff.AddCareRecipientAsync();
        await CompleteAsync(oneOff, caregiver, profileId, otherRecipient, Start.AddDays(14));

        var insights = await InsightsAsync(caregiver);

        Assert.Equal(3, insights!.CompletedLifetime);
        Assert.Equal(1, insights.RepeatFamilies);
    }

    /// <summary>
    /// The response carries counts and rates. A caregiver learns that a family came back,
    /// never which family, and nothing about a care recipient reaches this screen at all.
    /// </summary>
    [Fact]
    public async Task Insights_name_nobody()
    {
        var (profileId, caregiver) = await SubscribedCaregiverAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        await requester.CreateBookingAsync(profileId, recipientId, Start, Start.AddHours(4));

        var body = await (await caregiver.GetAsync("/api/v1/caregivers/me/insights"))
            .Content.ReadAsStringAsync();

        Assert.DoesNotContain("@example.com", body);
        Assert.DoesNotContain("Bedtime", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("careRecipient", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("careRequesterProfileId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instructions", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---- helpers ----

    /// <summary>A pending request from a fresh family.</summary>
    private async Task<Guid> RequestAsync(Guid caregiverProfileId, DateTime start)
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var booking = await requester.CreateBookingAsync(
            caregiverProfileId, recipientId, start, start.AddHours(4));

        return booking.Id;
    }

    private async Task CompleteAsync(
        HttpClient requester,
        HttpClient caregiver,
        Guid caregiverProfileId,
        Guid recipientId,
        DateTime start)
    {
        var booking = await requester.CreateBookingAsync(
            caregiverProfileId, recipientId, start, start.AddHours(4));

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - start);

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null))
            .EnsureSuccessStatusCode();
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null))
            .EnsureSuccessStatusCode();
    }
}
