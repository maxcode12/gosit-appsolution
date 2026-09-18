using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Disclosure of a care-needs profile to a booked caregiver.
///
/// This is the most sensitive read in the product — a child's allergies, triggers and
/// medical instructions — so the rules get their own suite rather than a line in the
/// booking tests. Every case below is a way the record could leak if the handler were
/// wrong.
/// </summary>
[Collection(nameof(ApiCollection))]
public class BookingCareNeedsTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Wednesday, 15);
    private static readonly DateTime End = Start.AddHours(4);

    private static object CareNeeds() => new
    {
        mobilityNeeds = (string?)null,
        communicationStyle = (string?)null,
        sensoryNeeds = (string?)null,
        behaviourSupportNotes = (string?)null,
        dietaryRestrictions = (string?)null,
        allergies = "Peanuts",
        toiletingAssistance = false,
        feedingAssistance = true,
        routinePreferences = (string?)null,
        triggers = "Sudden changes of plan",
        calmingStrategies = "Dim the lights",
        emergencyInstructions = "Call me, then Dr. Okafor",
        requiredSkills = new[] { "FIRST_AID" },
    };

    /// <summary>A requester with a recipient whose care needs are on file.</summary>
    private async Task<(HttpClient Client, Guid RecipientId)> RequesterWithNeedsAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        var recipientId = await client.AddCareRecipientAsync();

        var update = await client.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            CareNeeds());

        update.EnsureSuccessStatusCode();

        return (client, recipientId);
    }

    private static string Url(Guid bookingId, Guid recipientId) =>
        $"/api/v1/bookings/{bookingId}/recipients/{recipientId}/care-needs";

    [Fact]
    public async Task A_caregiver_can_read_care_needs_once_they_have_accepted()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterWithNeedsAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        var body = await caregiver.GetFromJsonAsync<CareNeedsBody>(Url(booking.Id, recipientId));

        Assert.Equal("Peanuts", body!.CareNeeds.Allergies);
        Assert.True(body.CareNeeds.FeedingAssistance);
        Assert.Equal("Sam", body.FirstName);
    }

    /// <summary>
    /// The central rule. A pending request must disclose nothing, or a caregiver could
    /// harvest care records simply by being sent requests they never accept.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_read_care_needs_while_the_booking_is_only_pending()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterWithNeedsAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var response = await caregiver.GetAsync(Url(booking.Id, recipientId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_who_declined_cannot_read_care_needs()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterWithNeedsAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        (await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{booking.Id}/decline", new { reason = "Not available" }))
            .EnsureSuccessStatusCode();

        var response = await caregiver.GetAsync(Url(booking.Id, recipientId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// An unrelated caregiver, with their own accepted booking elsewhere, must not be
    /// able to read a record by guessing a booking and recipient id.
    /// </summary>
    [Fact]
    public async Task A_caregiver_on_a_different_booking_cannot_read_care_needs()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterWithNeedsAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var owner = await factory.CaregiverClientForAsync(caregiverId);
        (await owner.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        var otherCaregiverId = await factory.CreateBookableCaregiverAsync();
        var stranger = await factory.CaregiverClientForAsync(otherCaregiverId);

        var response = await stranger.GetAsync(Url(booking.Id, recipientId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A recipient the caller owns, but who is not named on this booking, must not be
    /// readable through it — otherwise one accepted booking would expose every child
    /// in that family.
    /// </summary>
    [Fact]
    public async Task A_recipient_not_named_on_the_booking_is_not_readable_through_it()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, bookedRecipientId) = await RequesterWithNeedsAsync();

        var siblingId = await requester.AddCareRecipientAsync("Ada");
        (await requester.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{siblingId}/care-needs", CareNeeds()))
            .EnsureSuccessStatusCode();

        var booking = await requester.CreateBookingAsync(caregiverId, bookedRecipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        var response = await caregiver.GetAsync(Url(booking.Id, siblingId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>The owner may read their own record at any point in the lifecycle.</summary>
    [Fact]
    public async Task The_requester_can_read_their_own_record_while_still_pending()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterWithNeedsAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var body = await requester.GetFromJsonAsync<CareNeedsBody>(Url(booking.Id, recipientId));

        Assert.Equal("Peanuts", body!.CareNeeds.Allergies);
    }

    /// <summary>
    /// Who opened a child's record, and when, is the first question an incident
    /// investigation asks. Every disclosure must leave a trail.
    /// </summary>
    [Fact]
    public async Task Disclosure_to_a_caregiver_is_audited()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var (requester, recipientId) = await RequesterWithNeedsAsync();
        var booking = await requester.CreateBookingAsync(caregiverId, recipientId, Start, End);

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
            .EnsureSuccessStatusCode();

        (await caregiver.GetAsync(Url(booking.Id, recipientId))).EnsureSuccessStatusCode();

        var actions = await factory.ReadAuditActionsAsync(recipientId);

        Assert.Contains("DisclosedToCaregiver", actions);
    }

    private sealed record CareNeedsBody(
        Guid CareRecipientId,
        string FirstName,
        int Age,
        string RecipientType,
        CareNeedsDetail CareNeeds);

    private sealed record CareNeedsDetail(
        string? MobilityNeeds,
        string? CommunicationStyle,
        string? SensoryNeeds,
        string? BehaviourSupportNotes,
        string? DietaryRestrictions,
        string? Allergies,
        bool ToiletingAssistance,
        bool FeedingAssistance,
        string? RoutinePreferences,
        string? Triggers,
        string? CalmingStrategies,
        string? EmergencyInstructions,
        IReadOnlyList<string> RequiredSkills,
        DateTime? UpdatedAtUtc);
}
