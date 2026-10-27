using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Helpers for registering an account and getting an authenticated client, so the
/// profile tests read as the scenario they describe rather than as setup.
/// </summary>
public static class ProfileTestClient
{
    private const string ValidPassword = "correct-horse-battery-staple";

    public sealed record Tokens(
        string AccessToken,
        DateTime AccessTokenExpiresAtUtc,
        string RefreshToken,
        Guid UserId,
        string Role);

    public static async Task<HttpClient> RegisterCareRequesterAsync(this GoSitApiFactory factory) =>
        await factory.RegisterAsync("CareRequester");

    public static async Task<HttpClient> RegisterCaregiverAsync(this GoSitApiFactory factory) =>
        await factory.RegisterAsync("Caregiver");

    /// <summary>
    /// Registers a care requester and reports the email too, for tests that need to look
    /// the account up — the billing tests match a webhook to an account by user id.
    /// </summary>
    public static async Task<(HttpClient Client, string Email)> RegisterRequesterAsync(
        this GoSitApiFactory factory)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var client = await factory.RegisterAsync("CareRequester", email);

        return (client, email);
    }

    public static async Task<HttpClient> RegisterAsync(
        this GoSitApiFactory factory,
        string role,
        string? email = null)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = email ?? $"user-{Guid.NewGuid():N}@example.com",
            password = ValidPassword,
            firstName = "Ada",
            lastName = "Lovelace",
            role,
        });

        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<Tokens>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }

    /// <summary>Creates the minimum care-requester profile the recipient endpoints need.</summary>
    public static async Task CreateCareRequesterProfileAsync(
        this HttpClient client,
        string postalCode = "M5S 1M2")
    {
        var response = await client.PutAsJsonAsync("/api/v1/care-requesters/me", new
        {
            phoneNumber = "416-555-0142",
            aboutMe = (string?)null,
            address = new
            {
                line1 = "12 Bloor St W",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode,
                country = "CA",
            },
        });

        response.EnsureSuccessStatusCode();
    }

    public static async Task<Guid> AddCareRecipientAsync(this HttpClient client, string firstName = "Sam")
    {
        var response = await client.PostAsJsonAsync("/api/v1/care-requesters/me/care-recipients", new
        {
            firstName,
            lastName = "Okafor",
            dateOfBirth = "2018-04-02",
            recipientType = "Child",
        });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CareRecipientBody>();
        return created!.Id;
    }

    public sealed record CareRecipientBody(
        Guid Id,
        string FirstName,
        string? LastName,
        DateOnly DateOfBirth,
        int Age,
        string RecipientType,
        CareNeedsBody CareNeeds);

    public sealed record CareNeedsBody(
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
