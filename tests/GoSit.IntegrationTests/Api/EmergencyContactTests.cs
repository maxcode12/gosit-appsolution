using System.Net;
using System.Net.Http.Json;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// The people a caregiver rings when the family cannot be reached.
///
/// `EmergencyContact` has been in the domain since Phase 2, with add and remove on the
/// aggregate and an `emergencyContacts` array on the profile response — and no endpoint
/// that could put anything in it. The array was permanently empty. The class comment said
/// contact details are "released only to a caregiver on a confirmed booking", and nothing
/// released them to a caregiver at all: someone alone in a house with a child and an
/// unanswered phone had the written emergency instructions and no person to call.
///
/// The disclosure rules are the point, so most of this is about who cannot see them.
/// </summary>
[Collection(nameof(ApiCollection))]
public class EmergencyContactTests(GoSitApiFactory factory)
{
    private static readonly DateTime Start = NextWeekdayAtUtc(DayOfWeek.Thursday, 15);

    private sealed record ContactBody(Guid Id, string FullName, string Relationship, string PhoneNumber);

    private sealed record ContactsBody(IReadOnlyList<ContactBody> Contacts);

    private sealed record ProfileBody(IReadOnlyList<ContactBody> EmergencyContacts);

    private static Task<HttpResponseMessage> AddContactAsync(
        HttpClient requester,
        string fullName = "Miriam Okonjo",
        string relationship = "Grandmother",
        string phoneNumber = "416-555-0148") =>
        requester.PostAsJsonAsync(
            "/api/v1/care-requesters/me/emergency-contacts",
            new { fullName, relationship, phoneNumber });

    private static Task<ContactsBody?> ContactsOnBookingAsync(HttpClient client, Guid bookingId) =>
        client.GetFromJsonAsync<ContactsBody>($"/api/v1/bookings/{bookingId}/emergency-contacts");

    /// <summary>A family, a caregiver, and a booking in whatever state is asked for.</summary>
    private async Task<(HttpClient Requester, HttpClient Caregiver, Guid BookingId)> BookingAsync(
        bool accepted)
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();

        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();
        var recipientId = await requester.AddCareRecipientAsync();

        (await AddContactAsync(requester)).EnsureSuccessStatusCode();

        var booking = await requester.CreateBookingAsync(
            caregiverId, recipientId, Start, Start.AddHours(4));

        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        if (accepted)
        {
            (await caregiver.PostAsync($"/api/v1/bookings/{booking.Id}/accept", null))
                .EnsureSuccessStatusCode();
        }

        return (requester, caregiver, booking.Id);
    }

    // ---- recording them ----

    /// <summary>The gap that made everything else moot: nothing could create one.</summary>
    [Fact]
    public async Task A_family_can_record_an_emergency_contact()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var response = await AddContactAsync(requester);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var profile = await requester.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");

        var contact = Assert.Single(profile!.EmergencyContacts);

        Assert.Equal("Miriam Okonjo", contact.FullName);
        Assert.Equal("Grandmother", contact.Relationship);
    }

    /// <summary>
    /// The relationship is what tells a caregiver which of five people to ring first, and
    /// whether this is someone who may collect a child.
    /// </summary>
    [Fact]
    public async Task A_contact_without_a_relationship_is_refused()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var response = await AddContactAsync(requester, relationship: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_family_can_remove_a_contact()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        await requester.CreateCareRequesterProfileAsync();

        var created = await (await AddContactAsync(requester))
            .Content.ReadFromJsonAsync<ContactBody>();

        (await requester.DeleteAsync(
            $"/api/v1/care-requesters/me/emergency-contacts/{created!.Id}"))
            .EnsureSuccessStatusCode();

        var profile = await requester.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");

        Assert.Empty(profile!.EmergencyContacts);
    }

    /// <summary>
    /// Another family's contact id is "does not exist" rather than forbidden — a 403
    /// would confirm the id is real.
    /// </summary>
    [Fact]
    public async Task One_family_cannot_remove_anothers_contact()
    {
        var owner = await factory.RegisterCareRequesterAsync();
        await owner.CreateCareRequesterProfileAsync();

        var created = await (await AddContactAsync(owner))
            .Content.ReadFromJsonAsync<ContactBody>();

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        var response = await stranger.DeleteAsync(
            $"/api/v1/care-requesters/me/emergency-contacts/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And it is still there.
        var profile = await owner.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");
        Assert.Single(profile!.EmergencyContacts);
    }

    // ---- disclosing them ----

    /// <summary>
    /// The reason the feature exists. A caregiver who has taken the booking on can reach
    /// someone.
    /// </summary>
    [Fact]
    public async Task A_caregiver_on_an_accepted_booking_gets_the_contacts()
    {
        var (_, caregiver, bookingId) = await BookingAsync(accepted: true);

        var contacts = await ContactsOnBookingAsync(caregiver, bookingId);

        var contact = Assert.Single(contacts!.Contacts);

        Assert.Equal("Miriam Okonjo", contact.FullName);
        Assert.False(string.IsNullOrWhiteSpace(contact.PhoneNumber));
    }

    /// <summary>
    /// The rule that stops the feature becoming a phone-number harvester: a caregiver
    /// could otherwise receive requests they never intend to accept and read a family's
    /// contacts from each one.
    /// </summary>
    [Fact]
    public async Task A_pending_request_discloses_nothing()
    {
        var (_, caregiver, bookingId) = await BookingAsync(accepted: false);

        var response = await caregiver.GetAsync($"/api/v1/bookings/{bookingId}/emergency-contacts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_who_declined_gets_nothing()
    {
        var (_, caregiver, bookingId) = await BookingAsync(accepted: false);

        (await caregiver.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/decline", new { reason = "Not free that evening." }))
            .EnsureSuccessStatusCode();

        var response = await caregiver.GetAsync($"/api/v1/bookings/{bookingId}/emergency-contacts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_with_no_part_in_the_booking_gets_nothing()
    {
        var (_, _, bookingId) = await BookingAsync(accepted: true);

        var otherCaregiverId = await factory.CreateBookableCaregiverAsync();
        var stranger = await factory.CaregiverClientForAsync(otherCaregiverId);

        var response = await stranger.GetAsync($"/api/v1/bookings/{bookingId}/emergency-contacts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Deliberately still readable after check-out. A caregiver may need to reach someone
    /// about what happened during a booking that has just ended, and cutting them off at
    /// check-out would take the number away at the moment it is wanted.
    /// </summary>
    [Fact]
    public async Task A_finished_booking_still_discloses_the_contacts()
    {
        var (_, caregiver, bookingId) = await BookingAsync(accepted: true);

        await factory.ShiftScheduleAsync(bookingId, DateTime.UtcNow - Start);

        (await caregiver.PostAsync($"/api/v1/bookings/{bookingId}/check-in", null))
            .EnsureSuccessStatusCode();
        (await caregiver.PostAsync($"/api/v1/bookings/{bookingId}/check-out", null))
            .EnsureSuccessStatusCode();

        var contacts = await ContactsOnBookingAsync(caregiver, bookingId);

        Assert.Single(contacts!.Contacts);
    }

    /// <summary>
    /// Who was handed a family's contacts, and when, is what an incident investigation
    /// asks. A read by the family itself is recorded differently from a disclosure.
    /// </summary>
    [Fact]
    public async Task Disclosing_the_contacts_to_a_caregiver_is_audited()
    {
        var (_, caregiver, bookingId) = await BookingAsync(accepted: true);

        (await caregiver.GetAsync($"/api/v1/bookings/{bookingId}/emergency-contacts"))
            .EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var actions = await context.AuditEvents
            .Where(a => a.EntityType == "EmergencyContact")
            .Select(a => a.Action)
            .ToListAsync();

        Assert.Contains("DisclosedToCaregiver", actions);
    }
}
