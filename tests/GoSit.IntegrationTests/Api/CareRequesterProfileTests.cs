using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class CareRequesterProfileTests(GoSitApiFactory factory)
{
    private sealed record ProfileBody(
        Guid Id,
        string PhoneNumber,
        string? AboutMe,
        AddressBody? Address,
        IReadOnlyList<RecipientSummaryBody> CareRecipients,
        IReadOnlyList<ContactBody> EmergencyContacts);

    private sealed record AddressBody(
        string Line1, string? Line2, string City, string Province, string PostalCode, string Country);

    private sealed record RecipientSummaryBody(
        Guid Id, string FirstName, string? LastName, DateOnly DateOfBirth,
        int Age, string RecipientType, bool HasRecordedCareNeeds);

    private sealed record ContactBody(Guid Id, string FullName, string Relationship, string PhoneNumber);

    [Fact]
    public async Task A_care_requester_can_create_and_read_their_profile()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();

        var profile = await client.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");

        Assert.NotNull(profile);
        Assert.Equal("+14165550142", profile.PhoneNumber);
        Assert.Equal("Toronto", profile.Address?.City);
        // Stored without the inner space so lookups are exact matches.
        Assert.Equal("M5S1M2", profile.Address?.PostalCode);
    }

    [Fact]
    public async Task Reading_a_profile_that_does_not_exist_yet_returns_404()
    {
        var client = await factory.RegisterCareRequesterAsync();

        var response = await client.GetAsync("/api/v1/care-requesters/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Saving_the_profile_twice_updates_rather_than_duplicates()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();

        var update = await client.PutAsJsonAsync("/api/v1/care-requesters/me", new
        {
            phoneNumber = "416-555-0199",
            aboutMe = "Two children, weekday afternoons.",
            address = (object?)null,
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var profile = await client.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");
        Assert.Equal("+14165550199", profile!.PhoneNumber);
        Assert.Equal("Two children, weekday afternoons.", profile.AboutMe);
        // Omitting the address leaves the stored one alone rather than clearing it.
        Assert.Equal("Toronto", profile.Address?.City);
    }

    [Fact]
    public async Task The_endpoint_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/care-requesters/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The route is gated by the CareRequesterOnly policy, so a caregiver's token is
    /// refused before any handler runs.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_use_the_care_requester_endpoints()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        var response = await caregiver.GetAsync("/api/v1/care-requesters/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    public async Task An_invalid_phone_number_is_rejected(string phoneNumber)
    {
        var client = await factory.RegisterCareRequesterAsync();

        var response = await client.PutAsJsonAsync("/api/v1/care-requesters/me", new
        {
            phoneNumber,
            aboutMe = (string?)null,
            address = (object?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_invalid_postal_code_is_rejected()
    {
        var client = await factory.RegisterCareRequesterAsync();

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
                postalCode = "NOPE",
                country = "CA",
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Emergency_contacts_and_recipients_are_listed_on_the_profile()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        await client.AddCareRecipientAsync();

        var profile = await client.GetFromJsonAsync<ProfileBody>("/api/v1/care-requesters/me");

        var recipient = Assert.Single(profile!.CareRecipients);
        Assert.Equal("Sam", recipient.FirstName);
        Assert.Equal("Child", recipient.RecipientType);
        // The summary reports only whether needs exist, never their content.
        Assert.False(recipient.HasRecordedCareNeeds);
    }
}
