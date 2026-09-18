using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class CareRecipientTests(GoSitApiFactory factory)
{
    private static object CareNeedsPayload(string? allergies = "Peanuts") => new
    {
        mobilityNeeds = (string?)null,
        communicationStyle = "Prefers short, clear instructions.",
        sensoryNeeds = (string?)null,
        behaviourSupportNotes = (string?)null,
        dietaryRestrictions = (string?)null,
        allergies,
        toiletingAssistance = false,
        feedingAssistance = true,
        routinePreferences = (string?)null,
        triggers = "Loud noises",
        calmingStrategies = "Quiet room, familiar blanket.",
        emergencyInstructions = "Call the parent, then the family doctor.",
        requiredSkills = new[] { "first_aid", "CPR" },
    };

    [Fact]
    public async Task A_care_recipient_can_be_added_and_read_back()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();

        var recipientId = await client.AddCareRecipientAsync();

        var recipient = await client.GetFromJsonAsync<ProfileTestClient.CareRecipientBody>(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");

        Assert.NotNull(recipient);
        Assert.Equal("Sam", recipient.FirstName);
        Assert.Equal("Child", recipient.RecipientType);
    }

    [Fact]
    public async Task Adding_a_recipient_without_a_profile_returns_404()
    {
        var client = await factory.RegisterCareRequesterAsync();

        var response = await client.PostAsJsonAsync("/api/v1/care-requesters/me/care-recipients", new
        {
            firstName = "Sam",
            lastName = "Okafor",
            dateOfBirth = "2018-04-02",
            recipientType = "Child",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_minor_cannot_be_recorded_as_a_senior()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();

        var response = await client.PostAsJsonAsync("/api/v1/care-requesters/me/care-recipients", new
        {
            firstName = "Sam",
            lastName = "Okafor",
            dateOfBirth = "2018-04-02",
            recipientType = "Senior",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Care_needs_can_be_recorded_and_read_by_the_owner()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        var recipientId = await client.AddCareRecipientAsync();

        var update = await client.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            CareNeedsPayload());

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var recipient = await client.GetFromJsonAsync<ProfileTestClient.CareRecipientBody>(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");

        Assert.Equal("Peanuts", recipient!.CareNeeds.Allergies);
        Assert.True(recipient.CareNeeds.FeedingAssistance);
        // Skill codes are normalized so eligibility checks can compare them directly.
        Assert.Equal(["FIRST_AID", "CPR"], recipient.CareNeeds.RequiredSkills);
    }

    [Fact]
    public async Task Updating_care_needs_replaces_the_previous_values()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        var recipientId = await client.AddCareRecipientAsync();

        await client.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            CareNeedsPayload());

        await client.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            CareNeedsPayload(allergies: null));

        var recipient = await client.GetFromJsonAsync<ProfileTestClient.CareRecipientBody>(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");

        // Clearing an allergy must actually clear it.
        Assert.Null(recipient!.CareNeeds.Allergies);
    }

    /// <summary>
    /// The central privacy rule for this phase: one care requester must not be able to
    /// reach another's care recipient, and the response must not confirm the id exists.
    /// </summary>
    [Fact]
    public async Task Another_care_requester_cannot_read_someone_elses_care_recipient()
    {
        var owner = await factory.RegisterCareRequesterAsync();
        await owner.CreateCareRequesterProfileAsync();
        var recipientId = await owner.AddCareRecipientAsync();
        await owner.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            CareNeedsPayload());

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        var response = await stranger.GetAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");

        // 404, not 403 — a 403 would confirm that this id belongs to someone.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Peanuts", body);
        Assert.DoesNotContain("Loud noises", body);
    }

    [Fact]
    public async Task Another_care_requester_cannot_overwrite_someone_elses_care_needs()
    {
        var owner = await factory.RegisterCareRequesterAsync();
        await owner.CreateCareRequesterProfileAsync();
        var recipientId = await owner.AddCareRecipientAsync();
        await owner.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            CareNeedsPayload());

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        var response = await stranger.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            CareNeedsPayload(allergies: "Injected by an attacker"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The owner's data must be untouched.
        var recipient = await owner.GetFromJsonAsync<ProfileTestClient.CareRecipientBody>(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");
        Assert.Equal("Peanuts", recipient!.CareNeeds.Allergies);
    }

    [Fact]
    public async Task Another_care_requester_cannot_delete_someone_elses_care_recipient()
    {
        var owner = await factory.RegisterCareRequesterAsync();
        await owner.CreateCareRequesterProfileAsync();
        var recipientId = await owner.AddCareRecipientAsync();

        var stranger = await factory.RegisterCareRequesterAsync();
        await stranger.CreateCareRequesterProfileAsync();

        var response = await stranger.DeleteAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stillThere = await owner.GetAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_reach_care_recipient_routes_at_all()
    {
        var owner = await factory.RegisterCareRequesterAsync();
        await owner.CreateCareRequesterProfileAsync();
        var recipientId = await owner.AddCareRecipientAsync();

        var caregiver = await factory.RegisterCaregiverAsync();

        var response = await caregiver.GetAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/care-requesters/me/care-recipients/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_owner_can_delete_a_care_recipient()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        var recipientId = await client.AddCareRecipientAsync();

        var deleted = await client.DeleteAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await client.GetAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task An_over_long_care_note_is_rejected()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        var recipientId = await client.AddCareRecipientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/care-requesters/me/care-recipients/{recipientId}/care-needs",
            new
            {
                triggers = new string('x', 2001),
                toiletingAssistance = false,
                feedingAssistance = false,
                requiredSkills = Array.Empty<string>(),
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
