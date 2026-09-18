using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.SearchTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class CaregiverDetailTests(GoSitApiFactory factory)
{
    private async Task<HttpClient> RequesterAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        return client;
    }

    [Fact]
    public async Task A_listable_caregiver_can_be_viewed_in_detail()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING", "AFTER_SCHOOL_CARE"],
            certificationCodes: ["VULNERABLE_SECTOR"],
            availability: [(DayOfWeek.Monday, "09:00:00", "17:00:00")]);

        var client = await RequesterAsync();

        var detail = await client.GetFromJsonAsync<DetailBody>($"/api/v1/caregivers/{caregiverId}");

        Assert.NotNull(detail);
        Assert.Equal("Ada", detail.FirstName);
        Assert.Equal("L", detail.LastInitial);
        Assert.True(detail.IsVerified);
        Assert.Equal(2, detail.Services.Count);
        Assert.Contains(detail.Services, s => s.Code == "BABYSITTING" && s.CategoryCode == "CHILD_CARE");
        Assert.Single(detail.Availability);
        Assert.False(detail.IsFavorite);
    }

    /// <summary>
    /// The detail page is the richest public view of a caregiver, so it is the one most
    /// worth checking for leaks.
    /// </summary>
    [Fact]
    public async Task Detail_never_exposes_an_address_email_or_certification_document()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();
        var body = await client.GetStringAsync($"/api/v1/caregivers/{caregiverId}");

        Assert.Contains("Toronto, ON M5S", body);
        Assert.DoesNotContain("Bloor", body);
        Assert.DoesNotContain("@example.com", body);
        Assert.DoesNotContain("documentReference", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Only_currently_valid_certifications_are_advertised()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();
        var detail = await client.GetFromJsonAsync<DetailBody>($"/api/v1/caregivers/{caregiverId}");

        Assert.Equal(["VULNERABLE_SECTOR"], detail!.CertificationCodes);
    }

    /// <summary>
    /// An unverified caregiver must be indistinguishable from one that does not exist,
    /// so the endpoint cannot be used to discover accounts or infer a rejection.
    /// </summary>
    [Fact]
    public async Task An_unverified_caregiver_detail_is_reported_as_not_found()
    {
        var caregiver = await factory.RegisterCaregiverAsync();
        var created = await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ready to work.",
            yearsOfExperience = 4,
            hourlyRate = 22m,
            address = new
            {
                line1 = "1 Yonge St",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5E 1E5",
                country = "CA",
            },
            serviceRadiusKm = 20,
        });
        var unverifiedId = (await created.Content.ReadFromJsonAsync<CaregiverBody>())!.Id;

        var client = await RequesterAsync();

        var response = await client.GetAsync($"/api/v1/caregivers/{unverifiedId}");
        var unknown = await client.GetAsync($"/api/v1/caregivers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(unknown.StatusCode, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_browse_other_caregivers()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var otherCaregiver = await factory.RegisterCaregiverAsync();

        var response = await otherCaregiver.GetAsync($"/api/v1/caregivers/{caregiverId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

[Collection(nameof(ApiCollection))]
public class FavoritesTests(GoSitApiFactory factory)
{
    private async Task<HttpClient> RequesterAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        return client;
    }

    private static async Task<IReadOnlyList<SearchResultBody>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<SearchResultBody>>(
            "/api/v1/care-requesters/me/favorites"))!;

    [Fact]
    public async Task A_caregiver_can_be_saved_and_listed()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var client = await RequesterAsync();

        var saved = await client.PutAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}", null);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        var favorites = await ListAsync(client);

        var favorite = Assert.Single(favorites);
        Assert.Equal(caregiverId, favorite.CaregiverProfileId);
        Assert.True(favorite.IsFavorite);
    }

    [Fact]
    public async Task Saved_caregivers_are_flagged_in_search_results()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["DEMENTIA_COMPANION_CARE"],
            certificationCodes: ["POLICE_CHECK", "DEMENTIA_CARE"]);
        var client = await RequesterAsync();
        await client.PutAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}", null);

        var results = await client.GetFromJsonAsync<SearchBody>(
            "/api/v1/caregivers/search?serviceCode=DEMENTIA_COMPANION_CARE");

        var result = results!.Results.Single(r => r.CaregiverProfileId == caregiverId);
        Assert.True(result.IsFavorite);
    }

    [Fact]
    public async Task Saving_the_same_caregiver_twice_is_not_an_error()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var client = await RequesterAsync();

        await client.PutAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}", null);
        var second = await client.PutAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}", null);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(await ListAsync(client));
    }

    [Fact]
    public async Task A_saved_caregiver_can_be_removed()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var client = await RequesterAsync();
        await client.PutAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}", null);

        var removed = await client.DeleteAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await ListAsync(client));
    }

    [Fact]
    public async Task Removing_a_caregiver_that_was_never_saved_succeeds()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var client = await RequesterAsync();

        var response = await client.DeleteAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task An_unverified_caregiver_cannot_be_saved()
    {
        var caregiver = await factory.RegisterCaregiverAsync();
        var created = await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ready to work.",
            yearsOfExperience = 4,
            hourlyRate = 22m,
            address = new
            {
                line1 = "1 Yonge St",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5E 1E5",
                country = "CA",
            },
            serviceRadiusKm = 20,
        });
        var unverifiedId = (await created.Content.ReadFromJsonAsync<CaregiverBody>())!.Id;

        var client = await RequesterAsync();

        var response = await client.PutAsync(
            $"/api/v1/care-requesters/me/favorites/{unverifiedId}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// One requester's saved list must be invisible to another, and unchangeable by them.
    /// </summary>
    [Fact]
    public async Task Favorites_are_private_to_the_requester_who_saved_them()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var owner = await RequesterAsync();
        await owner.PutAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}", null);

        var stranger = await RequesterAsync();

        Assert.Empty(await ListAsync(stranger));

        // Removing through the stranger's own list must not touch the owner's.
        await stranger.DeleteAsync($"/api/v1/care-requesters/me/favorites/{caregiverId}");
        Assert.Single(await ListAsync(owner));
    }

    [Fact]
    public async Task A_caregiver_cannot_use_the_favorites_endpoints()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        var response = await caregiver.GetAsync("/api/v1/care-requesters/me/favorites");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_requester_without_a_profile_has_an_empty_saved_list()
    {
        var client = await factory.RegisterCareRequesterAsync();

        Assert.Empty(await ListAsync(client));
    }
}

[Collection(nameof(ApiCollection))]
public class CareServiceCatalogueTests(GoSitApiFactory factory)
{
    private sealed record CatalogueBody(IReadOnlyList<CategoryBody> Categories);

    private sealed record CategoryBody(
        string Code, string Name, int DisplayOrder, IReadOnlyList<ServiceDetailBody> Services);

    private sealed record ServiceDetailBody(
        string Code,
        string Name,
        string? Description,
        IReadOnlyList<string> RequiredCertificationCodes,
        IReadOnlyList<string> ApplicableRecipientTypes);

    /// <summary>
    /// Public reference data with no personal information — the marketing site needs it
    /// for service and location landing pages.
    /// </summary>
    [Fact]
    public async Task The_taxonomy_is_available_anonymously()
    {
        var client = factory.CreateClient();

        var catalogue = await client.GetFromJsonAsync<CatalogueBody>("/api/v1/care-services");

        Assert.NotNull(catalogue);
        Assert.Equal(5, catalogue.Categories.Count);
        Assert.Equal(
            ["CHILD_CARE", "SENIOR_CARE", "SPECIALIZED_SUPPORT", "FAMILY_SUPPORT", "PET_CARE"],
            catalogue.Categories.Select(c => c.Code));
    }

    [Fact]
    public async Task Services_carry_their_qualification_requirements()
    {
        var client = factory.CreateClient();

        var catalogue = await client.GetFromJsonAsync<CatalogueBody>("/api/v1/care-services");

        var overnight = catalogue!.Categories
            .SelectMany(c => c.Services)
            .Single(s => s.Code == "OVERNIGHT_CHILD_CARE");

        Assert.Equal(
            new[] { "VULNERABLE_SECTOR", "FIRST_AID", "CPR" }.Order(),
            overnight.RequiredCertificationCodes.Order());
        Assert.Equal(["Child"], overnight.ApplicableRecipientTypes);
    }

    [Fact]
    public async Task Specialized_support_applies_to_recipients_of_every_age()
    {
        var client = factory.CreateClient();

        var catalogue = await client.GetFromJsonAsync<CatalogueBody>("/api/v1/care-services");

        var autism = catalogue!.Categories
            .SelectMany(c => c.Services)
            .Single(s => s.Code == "AUTISM_SUPPORT");

        // Special needs are described per recipient, not restricted to children.
        Assert.Equal(new[] { "Child", "Adult", "Senior" }.Order(), autism.ApplicableRecipientTypes.Order());
    }
}
