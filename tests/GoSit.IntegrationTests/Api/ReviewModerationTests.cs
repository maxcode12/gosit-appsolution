using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Taking down an abusive review.
///
/// A published review used to be permanent. There was no way to remove one and no way to
/// tell anyone about it — a caregiver's rating is their income, and a retaliatory one-star
/// after a dispute, a comment naming a child, or a slur reached nobody who could act.
/// `POST /reports` reports a *user*, which leaves the words on the profile.
///
/// The two properties worth pinning are that hiding actually removes it from the profile
/// **and** from the rating, and that only the person it is about can report it.
/// </summary>
[Collection(nameof(ApiCollection))]
public class ReviewModerationTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Tuesday, 15);

    private sealed record ReviewBody(Guid Id, int Rating, string? Comment);

    private sealed record CaregiverReviewsBody(
        decimal? AverageRating, int ReviewCount, IReadOnlyList<ReviewBody> Reviews);

    private sealed record ReportedReviewBody(
        Guid ReviewId, string SubjectName, string AuthorName,
        int Rating, string? Comment, string? ReportReason);

    private Task<CaregiverReviewsBody?> PublicReviewsAsync(HttpClient client, Guid caregiverId) =>
        client.GetFromJsonAsync<CaregiverReviewsBody>(
            $"/api/v1/caregivers/{caregiverId}/reviews");

    /// <summary>
    /// A completed booking reviewed by both sides, so the pair publishes immediately.
    /// Returns the caregiver, and the id of the review written about them.
    /// </summary>
    private async Task<(Guid CaregiverId, HttpClient Caregiver, Guid ReviewId)> ReviewedAsync(
        int rating = 1,
        string comment = "Rude and left early. Would never use again.")
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var booking = await requester.CreateBookingAsync(
            caregiverId, recipientId, Start, Start.AddHours(4));

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        await factory.ShiftScheduleAsync(booking.Id, DateTime.UtcNow - Start);

        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-in", null))
            .EnsureSuccessStatusCode();
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/check-out", null))
            .EnsureSuccessStatusCode();

        // The family reviews the caregiver...
        var written = await (await requester.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/review", new { rating, comment }))
            .Content.ReadFromJsonAsync<ReviewBody>();

        // ...and the caregiver reviews the family, which publishes the pair.
        (await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/review", new { rating = 5, comment = "Fine." }))
            .EnsureSuccessStatusCode();

        return (caregiverId, caregiver, written!.Id);
    }

    // ---- reporting ----

    /// <summary>The discovery half: without it staff never learn a review is abusive.</summary>
    [Fact]
    public async Task The_caregiver_a_review_is_about_can_report_it()
    {
        var (_, caregiver, reviewId) = await ReviewedAsync();

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/reviews/{reviewId}/report",
            new { reason = "This is about a booking they cancelled, not one I worked." }))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        var queue = await admin.GetFromJsonAsync<List<ReportedReviewBody>>(
            "/api/v1/admin/reported-reviews?take=200");

        var row = Assert.Single(queue!, r => r.ReviewId == reviewId);

        // Both sides named: the decision is about words one real person wrote about
        // another, and the review's own text is what is being judged.
        Assert.False(string.IsNullOrWhiteSpace(row.SubjectName));
        Assert.False(string.IsNullOrWhiteSpace(row.AuthorName));
        Assert.Contains("Rude and left early", row.Comment);
        Assert.Contains("cancelled", row.ReportReason);
    }

    /// <summary>
    /// A marketplace where anyone can queue up anyone else's reviews is one where
    /// competitors do.
    /// </summary>
    [Fact]
    public async Task A_stranger_cannot_report_someone_elses_review()
    {
        var (_, _, reviewId) = await ReviewedAsync();

        var otherCaregiverId = await factory.CreateBookableCaregiverAsync();
        var stranger = await factory.CaregiverClientForAsync(otherCaregiverId);

        var response = await stranger.PostAsJsonAsync(
            $"/api/v1/reviews/{reviewId}/report", new { reason = "I do not like it." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reporting_without_a_reason_is_refused()
    {
        var (_, caregiver, reviewId) = await ReviewedAsync();

        var response = await caregiver.PostAsJsonAsync(
            $"/api/v1/reviews/{reviewId}/report", new { reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_queue_is_staff_only()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.GetAsync("/api/v1/admin/reported-reviews");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- the decision ----

    /// <summary>
    /// The half that matters. Hiding has to remove the review from the profile **and**
    /// from the rating — otherwise the words go and the one star goes on dragging the
    /// caregiver's average down, which removes only the evidence.
    /// </summary>
    [Fact]
    public async Task Hiding_a_review_removes_it_from_the_profile_and_the_rating()
    {
        var (caregiverId, caregiver, reviewId) = await ReviewedAsync(rating: 1);

        var before = await PublicReviewsAsync(caregiver, caregiverId);

        Assert.Contains(before!.Reviews, r => r.Id == reviewId);
        Assert.Equal(1, before.ReviewCount);
        Assert.Equal(1m, before.AverageRating);

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/reviews/{reviewId}/report", new { reason = "Names my child." }))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/reported-reviews/{reviewId}/moderate",
            new { hide = true, reason = "Comment named a child." }))
            .EnsureSuccessStatusCode();

        var after = await PublicReviewsAsync(caregiver, caregiverId);

        Assert.DoesNotContain(after!.Reviews, r => r.Id == reviewId);
        Assert.Equal(0, after.ReviewCount);
        Assert.Null(after.AverageRating);
    }

    [Fact]
    public async Task Hiding_without_a_reason_is_refused()
    {
        var (_, caregiver, reviewId) = await ReviewedAsync();

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/reviews/{reviewId}/report", new { reason = "Abusive." }))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/reported-reviews/{reviewId}/moderate",
            new { hide = true, reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Letting a review stand clears it from the queue without touching the profile. Most
    /// reports will end this way: a one-star someone disagrees with is not abuse.
    /// </summary>
    [Fact]
    public async Task A_review_allowed_to_stand_leaves_the_queue_and_stays_published()
    {
        var (caregiverId, caregiver, reviewId) = await ReviewedAsync();

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/reviews/{reviewId}/report", new { reason = "I disagree with it." }))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/reported-reviews/{reviewId}/moderate",
            new { hide = false, reason = (string?)null }))
            .EnsureSuccessStatusCode();

        var queue = await admin.GetFromJsonAsync<List<ReportedReviewBody>>(
            "/api/v1/admin/reported-reviews?take=200");

        Assert.DoesNotContain(queue!, r => r.ReviewId == reviewId);

        var reviews = await PublicReviewsAsync(caregiver, caregiverId);

        Assert.Contains(reviews!.Reviews, r => r.Id == reviewId);
    }

    [Fact]
    public async Task A_hidden_review_cannot_be_hidden_twice()
    {
        var (_, caregiver, reviewId) = await ReviewedAsync();

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/reviews/{reviewId}/report", new { reason = "Abusive." }))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();
        var body = new { hide = true, reason = "Contains a slur." };

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/reported-reviews/{reviewId}/moderate", body))
            .EnsureSuccessStatusCode();

        var second = await admin.PostAsJsonAsync(
            $"/api/v1/admin/reported-reviews/{reviewId}/moderate", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Staff can act on a review nobody reported — an abusive comment found while looking
    /// at something else should not need the subject to file a form first.
    /// </summary>
    [Fact]
    public async Task Staff_can_hide_a_review_that_was_never_reported()
    {
        var (caregiverId, caregiver, reviewId) = await ReviewedAsync();

        var admin = await factory.RegisterAdminAsync();

        (await admin.PostAsJsonAsync(
            $"/api/v1/admin/reported-reviews/{reviewId}/moderate",
            new { hide = true, reason = "Contains a slur." }))
            .EnsureSuccessStatusCode();

        var reviews = await PublicReviewsAsync(caregiver, caregiverId);

        Assert.DoesNotContain(reviews!.Reviews, r => r.Id == reviewId);
    }
}
