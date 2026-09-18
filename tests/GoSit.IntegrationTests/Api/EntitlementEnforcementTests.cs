using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Bookings;
using GoSit.Domain.Subscriptions;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// The half of subscriptions that had no effect.
///
/// Every feature code was declared by a plan, returned by the API, and listed on the
/// plan screen — and consulted by nothing. An account could pay for "unlimited
/// favourites" while having exactly the same limit as everyone else, which is a promise
/// the product was taking money for and not keeping.
///
/// These tests pin each gate to the behaviour a subscriber is being sold. They run with
/// no billing provider configured, so subscribing grants the plan immediately — see
/// <see cref="SubscriptionTests"/>.
/// </summary>
[Collection(nameof(ApiCollection))]
public class EntitlementEnforcementTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);
    private static readonly DateTime End = Start.AddHours(4);

    private static async Task SubscribeAsync(HttpClient client, string planCode)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode, promoCode = (string?)null });

        response.EnsureSuccessStatusCode();
    }

    // ---- UNLIMITED_FAVOURITES ----

    /// <summary>
    /// Saving beyond the free cap is refused, and the refusal says what the limit is and
    /// what lifts it. "You have reached a limit" with no number sends someone to support.
    /// </summary>
    [Fact]
    public async Task Saving_past_the_free_limit_is_refused_with_a_reason()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var saved = await SaveManyAsync(requester, EntitlementPolicyLimit);

        Assert.Equal(EntitlementPolicyLimit, saved);

        var oneMore = await factory.CreateBookableCaregiverAsync();
        var refused = await requester.PutAsync($"/api/v1/care-requesters/me/favorites/{oneMore}", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var problem = await refused.Content.ReadFromJsonAsync<ProblemBody>();

        Assert.Contains(EntitlementPolicyLimit.ToString(), problem!.Title);
        Assert.Contains("upgrade", problem.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_subscriber_can_save_past_the_free_limit()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        await SubscribeAsync(requester, "PLUS_REQUESTER");

        await SaveManyAsync(requester, EntitlementPolicyLimit);

        var oneMore = await factory.CreateBookableCaregiverAsync();
        var response = await requester.PutAsync($"/api/v1/care-requesters/me/favorites/{oneMore}", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Re-saving something already saved changes nothing, so it must not be refused for
    /// being over a limit the request would not move.
    /// </summary>
    [Fact]
    public async Task Re_saving_an_existing_favourite_is_allowed_at_the_limit()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var first = await factory.CreateBookableCaregiverAsync();
        (await requester.PutAsync($"/api/v1/care-requesters/me/favorites/{first}", null)).EnsureSuccessStatusCode();

        await SaveManyAsync(requester, EntitlementPolicyLimit - 1);

        var again = await requester.PutAsync($"/api/v1/care-requesters/me/favorites/{first}", null);

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    // ---- ADVANCED_SEARCH_FILTERS ----

    /// <summary>
    /// Refused rather than silently ignored. Returning unfiltered results to someone who
    /// asked for "only caregivers with First Aid" would present them as filtered, which
    /// in a care marketplace is how the wrong person gets booked.
    /// </summary>
    [Theory]
    [InlineData("certifications=CPR")]
    [InlineData("minYearsOfExperience=5")]
    public async Task An_advanced_filter_is_refused_on_the_free_plan(string filter)
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.GetAsync($"/api/v1/caregivers/search?{filter}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("certifications=CPR")]
    [InlineData("minYearsOfExperience=5")]
    public async Task A_subscriber_may_use_an_advanced_filter(string filter)
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await SubscribeAsync(requester, "PLUS_REQUESTER");

        var response = await requester.GetAsync($"/api/v1/caregivers/search?{filter}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The filters everyone needs to find care at all stay free. Gating these would make
    /// the free product not a product — and rating is deliberately among them, because
    /// selling "only show well-reviewed caregivers" is selling safety back to families.
    /// </summary>
    [Theory]
    [InlineData("city=Toronto")]
    [InlineData("serviceCode=BABYSITTING")]
    [InlineData("maxHourlyRate=40")]
    [InlineData("minRating=4")]
    public async Task The_ordinary_filters_stay_free(string filter)
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.GetAsync($"/api/v1/caregivers/search?{filter}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- PROFILE_PROMOTION ----

    /// <summary>
    /// A promoted caregiver is ranked ahead of an equally-placed one. Both are created
    /// with no address, so neither has a distance and they sit in the same band — which
    /// is exactly where promotion is allowed to decide.
    /// </summary>
    [Fact]
    public async Task A_promoted_caregiver_outranks_an_equal_one()
    {
        var city = $"Rankville{Guid.NewGuid():N}"[..20];

        var plainId = await factory.CreateListableCaregiverAsync(
            city: city, serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var promotedId = await factory.CreateListableCaregiverAsync(
            city: city, serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var promoted = await factory.CaregiverClientForAsync(promotedId);
        await SubscribeAsync(promoted, "PRO_CAREGIVER");

        var requester = await factory.RegisterCareRequesterAsync();

        var results = await SearchAsync(requester, $"city={city}");

        Assert.Equal(2, results.Count);
        Assert.Equal(promotedId, results[0].CaregiverProfileId);
        Assert.Equal(plainId, results[1].CaregiverProfileId);
    }

    /// <summary>
    /// Promotion must not be able to buy its way past suitability. A caregiver who
    /// cannot provide the service is not ranked lower — they are not shown at all.
    /// </summary>
    [Fact]
    public async Task Promotion_does_not_surface_an_ineligible_caregiver()
    {
        var city = $"Eligibility{Guid.NewGuid():N}"[..20];

        var promotedId = await factory.CreateListableCaregiverAsync(
            city: city, serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var promoted = await factory.CaregiverClientForAsync(promotedId);
        await SubscribeAsync(promoted, "PRO_CAREGIVER");

        var requester = await factory.RegisterCareRequesterAsync();

        var results = await SearchAsync(requester, $"city={city}&serviceCode=DEMENTIA_COMPANION_CARE");

        Assert.DoesNotContain(results, r => r.CaregiverProfileId == promotedId);
    }

    // ---- PRIORITY_BOOKING_REQUESTS ----

    /// <summary>
    /// A subscriber's unanswered request is shown first. Both requests are pending, so
    /// nothing that the caregiver has already agreed to is being moved.
    /// </summary>
    [Fact]
    public async Task A_subscribers_request_is_shown_to_the_caregiver_first()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        // The list is ordered most-recent-first, so the later booking naturally leads.
        // The subscriber deliberately takes the *earlier* slot: without enforcement it
        // would come second, so first place can only be the plan doing its job.
        var plainBooking = await BookAsync(caregiverId, Start.AddDays(1), plan: null);
        var priorityBooking = await BookAsync(caregiverId, Start, plan: "PLUS_REQUESTER");

        var listed = await caregiver.GetFromJsonAsync<List<BookingBody>>(
            "/api/v1/bookings?status=Pending");

        Assert.Equal(2, listed!.Count);
        Assert.Equal(priorityBooking, listed[0].Id);
        Assert.Equal(plainBooking, listed[1].Id);
    }

    /// <summary>
    /// Work the caregiver has already accepted stays in schedule order. Reordering an
    /// agreed shift because of somebody's plan would make the day lie about itself.
    /// </summary>
    [Fact]
    public async Task Accepted_work_is_not_reordered_by_a_plan()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var acceptedFirst = await BookAsync(caregiverId, Start, plan: null);
        (await caregiver.PostAsync($"/api/v1/bookings/{acceptedFirst}/accept", null))
            .EnsureSuccessStatusCode();

        var acceptedSecond = await BookAsync(caregiverId, Start.AddDays(2), plan: "PLUS_REQUESTER");
        (await caregiver.PostAsync($"/api/v1/bookings/{acceptedSecond}/accept", null))
            .EnsureSuccessStatusCode();

        var listed = await caregiver.GetFromJsonAsync<List<BookingBody>>(
            "/api/v1/bookings?status=Accepted");

        // The repository orders a history list most-recent-first, and that is exactly what
        // comes back: the subscriber does not jump ahead of work already agreed.
        Assert.Equal(2, listed!.Count);
        Assert.Equal(acceptedSecond, listed[0].Id);
        Assert.Equal(acceptedFirst, listed[1].Id);
    }

    /// <summary>Priority changes the order, never who is answerable.</summary>
    [Fact]
    public async Task A_free_plan_request_is_still_listed_and_answerable()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var plainBooking = await BookAsync(caregiverId, Start, plan: null);
        await BookAsync(caregiverId, Start.AddDays(1), plan: "PLUS_REQUESTER");

        var listed = await caregiver.GetFromJsonAsync<List<BookingBody>>(
            "/api/v1/bookings?status=Pending");

        Assert.Contains(listed!, b => b.Id == plainBooking);

        var accepted = await caregiver.PostAsync($"/api/v1/bookings/{plainBooking}/accept", null);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    // ---- helpers ----

    /// <summary>Mirrors the server's own cap so a change to it fails these tests loudly.</summary>
    private const int EntitlementPolicyLimit = 25;

    private sealed record ProblemBody(string Title);

    private sealed record SearchResultBody(Guid CaregiverProfileId, string FirstName);

    private sealed record SearchBody(
        IReadOnlyList<SearchResultBody> Results, int TotalCount, int Page, int PageSize);

    private static async Task<IReadOnlyList<SearchResultBody>> SearchAsync(
        HttpClient client,
        string queryString)
    {
        var response = await client.GetAsync($"/api/v1/caregivers/search?{queryString}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SearchBody>())!.Results;
    }

    /// <summary>Saves <paramref name="count"/> distinct caregivers, returning how many stuck.</summary>
    private async Task<int> SaveManyAsync(HttpClient requester, int count)
    {
        var saved = 0;

        for (var i = 0; i < count; i++)
        {
            var caregiverId = await factory.CreateBookableCaregiverAsync();
            var response = await requester.PutAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}", null);

            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            saved++;
        }

        return saved;
    }

    /// <summary>A pending booking from a fresh requester, optionally on a paid plan.</summary>
    private async Task<Guid> BookAsync(Guid caregiverProfileId, DateTime start, string? plan)
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        if (plan is not null)
        {
            await SubscribeAsync(requester, plan);
        }

        var booking = await requester.CreateBookingAsync(
            caregiverProfileId, recipientId, start, start.AddHours(4));

        Assert.Equal(BookingStatus.Pending.ToString(), booking.Status);

        return booking.Id;
    }
}
