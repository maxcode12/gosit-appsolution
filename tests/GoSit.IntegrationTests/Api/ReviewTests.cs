using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;
using static GoSit.IntegrationTests.Api.TrustAndSafetyTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class ReviewTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Friday, 15);
    private static readonly DateTime End = Start.AddHours(4);

    private sealed record ReviewBody(
        Guid Id, Guid CareBookingId, int Rating, string? Comment,
        DateTime CreatedAtUtc, bool IsPublished);

    private sealed record CaregiverReviewsBody(
        decimal? AverageRating, int ReviewCount, IReadOnlyList<PublishedReviewBody> Reviews);

    private sealed record PublishedReviewBody(
        Guid Id, int Rating, string? Comment, string AuthorFirstName,
        string AuthorLastInitial, DateTime CreatedAtUtc);

    private async Task<(HttpClient Requester, HttpClient Caregiver, Guid CaregiverId, Guid BookingId)>
        CompletedAsync()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var bookingId = await factory.CompletedBookingAsync(
            requester, caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        return (requester, caregiver, caregiverId, bookingId);
    }

    private static Task<HttpResponseMessage> ReviewAsync(
        HttpClient client, Guid bookingId, int rating, string? comment) =>
        client.PostAsJsonAsync($"/api/v1/bookings/{bookingId}/review", new { rating, comment });

    [Fact]
    public async Task A_participant_can_review_a_completed_booking()
    {
        var (requester, _, _, bookingId) = await CompletedAsync();

        var response = await ReviewAsync(requester, bookingId, 5, "Wonderful with Sam.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var review = (await response.Content.ReadFromJsonAsync<ReviewBody>())!;
        Assert.Equal(5, review.Rating);
    }

    [Fact]
    public async Task A_booking_that_has_not_completed_cannot_be_reviewed()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var response = await ReviewAsync(requester, booking.Id, 5, null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Someone_outside_the_booking_cannot_review_it()
    {
        var (_, _, _, bookingId) = await CompletedAsync();

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        var response = await ReviewAsync(stranger, bookingId, 1, "Never happened.");

        // 404 rather than 403, so booking ids cannot be probed.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_participant_can_only_review_once()
    {
        var (requester, _, _, bookingId) = await CompletedAsync();
        await ReviewAsync(requester, bookingId, 5, null);

        var second = await ReviewAsync(requester, bookingId, 1, "Changed my mind.");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task An_out_of_range_rating_is_rejected(int rating)
    {
        var (requester, _, _, bookingId) = await CompletedAsync();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await ReviewAsync(requester, bookingId, rating, null)).StatusCode);
    }

    // ---- blind review ----

    /// <summary>
    /// The property that makes reviews safe to write: neither side can read the other's
    /// before committing to their own, so nobody has to weigh retaliation.
    /// </summary>
    [Fact]
    public async Task A_lone_review_is_not_visible_to_the_caregiver()
    {
        var (requester, _, caregiverId, bookingId) = await CompletedAsync();

        await ReviewAsync(requester, bookingId, 5, "Wonderful with Sam.");

        var published = await requester.GetFromJsonAsync<CaregiverReviewsBody>(
            $"/api/v1/caregivers/{caregiverId}/reviews");

        Assert.Empty(published!.Reviews);
        Assert.Null(published.AverageRating);
        Assert.Equal(0, published.ReviewCount);
    }

    [Fact]
    public async Task Both_reviews_publish_once_the_second_arrives()
    {
        var (requester, caregiver, caregiverId, bookingId) = await CompletedAsync();

        await ReviewAsync(requester, bookingId, 5, "Wonderful with Sam.");
        await ReviewAsync(caregiver, bookingId, 4, "Clear instructions.");

        var published = await requester.GetFromJsonAsync<CaregiverReviewsBody>(
            $"/api/v1/caregivers/{caregiverId}/reviews");

        // Only the review *about the caregiver* appears on their profile.
        var review = Assert.Single(published!.Reviews);
        Assert.Equal(5, review.Rating);
        Assert.Equal(5m, published.AverageRating);
        Assert.Equal(1, published.ReviewCount);
    }

    /// <summary>
    /// A single review must not stay hidden forever because the other side never
    /// responded — otherwise silence would suppress feedback.
    /// </summary>
    [Fact]
    public async Task The_sweep_publishes_a_lone_review_once_the_window_closes()
    {
        var (requester, _, caregiverId, bookingId) = await CompletedAsync();
        await ReviewAsync(requester, bookingId, 4, "Good.");

        await factory.ExpireReviewWindowAsync(bookingId);
        var result = await factory.RunReviewSweepAsync();

        Assert.True(result.Published >= 1);

        var published = await requester.GetFromJsonAsync<CaregiverReviewsBody>(
            $"/api/v1/caregivers/{caregiverId}/reviews");

        Assert.Single(published!.Reviews);
        Assert.Equal(4m, published.AverageRating);
    }

    /// <summary>
    /// The rating that feeds search ranking is recomputed from published reviews only.
    /// </summary>
    [Fact]
    public async Task Publishing_updates_the_rating_used_by_search()
    {
        var (requester, caregiver, caregiverId, bookingId) = await CompletedAsync();

        await ReviewAsync(requester, bookingId, 5, null);
        await ReviewAsync(caregiver, bookingId, 5, null);

        var detail = await requester.GetFromJsonAsync<SearchTestFixtures.DetailBody>(
            $"/api/v1/caregivers/{caregiverId}");

        Assert.Equal(5m, detail!.AverageRating);
        Assert.Equal(1, detail.ReviewCount);
    }

    [Fact]
    public async Task A_published_review_shows_the_author_by_first_name_and_initial_only()
    {
        var (requester, caregiver, caregiverId, bookingId) = await CompletedAsync();
        await ReviewAsync(requester, bookingId, 5, "Wonderful.");
        await ReviewAsync(caregiver, bookingId, 5, null);

        var body = await requester.GetStringAsync($"/api/v1/caregivers/{caregiverId}/reviews");

        Assert.DoesNotContain("@example.com", body);
        Assert.DoesNotContain("Bloor", body);

        var published = await requester.GetFromJsonAsync<CaregiverReviewsBody>(
            $"/api/v1/caregivers/{caregiverId}/reviews");

        var review = published!.Reviews.Single();
        Assert.Equal("Ada", review.AuthorFirstName);
        Assert.Equal("L", review.AuthorLastInitial);
    }

    [Fact]
    public async Task Reviewing_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/bookings/{Guid.NewGuid()}/review", new { rating = 5, comment = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
