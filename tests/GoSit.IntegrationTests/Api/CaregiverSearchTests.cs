using System.Net;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.SearchTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class CaregiverSearchTests(GoSitApiFactory factory)
{
    private async Task<HttpClient> RequesterAsync()
    {
        var client = await factory.RegisterCareRequesterAsync();
        await client.CreateCareRequesterProfileAsync();
        return client;
    }

    /// <summary>
    /// A requester on a paid plan, for the filters sold as ADVANCED_SEARCH_FILTERS.
    ///
    /// Those filters are refused on the free plan — see
    /// <see cref="EntitlementEnforcementTests"/>, which owns that rule. Here the plan is
    /// simply the price of exercising the filter itself.
    /// </summary>
    private async Task<HttpClient> SubscribedRequesterAsync()
    {
        var client = await RequesterAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/me/subscription",
            new { planCode = "PLUS_REQUESTER", promoCode = (string?)null });

        response.EnsureSuccessStatusCode();

        return client;
    }

    private static async Task<SearchBody> SearchAsync(HttpClient client, string queryString = "")
    {
        var response = await client.GetAsync($"/api/v1/caregivers/search{queryString}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchBody>())!;
    }

    /// <summary>
    /// Walks every page.
    ///
    /// The suite shares one database across the assembly, so caregivers accumulate as
    /// tests run. Asserting that a caregiver appears in the *first* page of an unfiltered
    /// search therefore passes or fails depending on how many other tests ran first —
    /// which is a property of the suite, not of search. Paging through asserts what the
    /// test actually means: that the caregiver is findable.
    /// </summary>
    private static async Task<List<SearchResultBody>> SearchEveryPageAsync(
        HttpClient client,
        string filters = "")
    {
        var all = new List<SearchResultBody>();
        var separator = string.IsNullOrEmpty(filters) ? "?" : "&";

        for (var page = 1; ; page++)
        {
            var body = await SearchAsync(client, $"{filters}{separator}page={page}&pageSize=50");

            all.AddRange(body.Results);

            if (body.Results.Count < 50 || all.Count >= body.TotalMatches)
            {
                return all;
            }
        }
    }

    [Fact]
    public async Task Search_returns_a_listable_caregiver()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var client = await RequesterAsync();

        var results = await SearchEveryPageAsync(client);

        Assert.Contains(results, r => r.CaregiverProfileId == caregiverId);
    }

    /// <summary>
    /// An unverified caregiver must never surface, however complete their profile.
    /// </summary>
    [Fact]
    public async Task An_unverified_caregiver_is_never_returned()
    {
        var caregiver = await factory.RegisterCaregiverAsync();
        var created = await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ready to work.",
            yearsOfExperience = 10,
            hourlyRate = 20m,
            address = new
            {
                line1 = "1 Yonge St",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5E 1E5",
                country = "CA",
            },
            serviceRadiusKm = 25,
        });
        var unverified = (await created.Content.ReadFromJsonAsync<CaregiverBody>())!.Id;

        var client = await RequesterAsync();
        var results = await SearchAsync(client);

        Assert.DoesNotContain(results.Results, r => r.CaregiverProfileId == unverified);
    }

    /// <summary>
    /// The core privacy rule for search: a result reveals a neighbourhood, never an address.
    /// </summary>
    [Fact]
    public async Task Results_expose_an_approximate_location_and_never_a_street_address()
    {
        // Scoped to a city of its own. Taking the first result of an unfiltered search
        // meant asserting on whichever caregiver some other test happened to add.
        var city = $"Privacyville{Guid.NewGuid():N}"[..20];

        await factory.CreateListableCaregiverAsync(
            city: city, postalCode: "M5S 1M2",
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();

        var response = await client.GetAsync($"/api/v1/caregivers/search?city={city}");
        var body = await response.Content.ReadAsStringAsync();
        var results = await SearchAsync(client, $"?city={city}");

        var result = results.Results.Single();
        Assert.Equal($"{city}, ON M5S", result.ApproximateLocation);

        Assert.DoesNotContain("Bloor", body);
        Assert.DoesNotContain("M5S1M2", body);
        Assert.DoesNotContain("line1", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@example.com", body);
    }

    [Fact]
    public async Task Results_carry_only_a_last_initial()
    {
        await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var client = await RequesterAsync();

        var results = await SearchAsync(client);
        var result = results.Results.First();

        Assert.Equal("Ada", result.FirstName);
        Assert.Equal("L", result.LastInitial);
    }

    /// <summary>
    /// Offering a service is not the same as qualifying for it.
    /// </summary>
    [Fact]
    public async Task A_caregiver_missing_a_required_certification_is_excluded_from_that_service()
    {
        var qualified = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["OVERNIGHT_CHILD_CARE"],
            certificationCodes: ["VULNERABLE_SECTOR", "FIRST_AID", "CPR"]);

        var unqualified = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["OVERNIGHT_CHILD_CARE"],
            certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();
        var results = await SearchAsync(client, "?serviceCode=OVERNIGHT_CHILD_CARE");

        Assert.Contains(results.Results, r => r.CaregiverProfileId == qualified);
        Assert.DoesNotContain(results.Results, r => r.CaregiverProfileId == unqualified);
    }

    [Fact]
    public async Task A_caregiver_who_does_not_offer_the_service_is_excluded()
    {
        var babysitter = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();
        var results = await SearchAsync(client, "?serviceCode=SENIOR_COMPANIONSHIP");

        Assert.DoesNotContain(results.Results, r => r.CaregiverProfileId == babysitter);
    }

    [Fact]
    public async Task An_unknown_service_code_returns_404()
    {
        var client = await RequesterAsync();

        var response = await client.GetAsync("/api/v1/caregivers/search?serviceCode=NOT_A_SERVICE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rate_and_experience_filters_are_applied()
    {
        // A city of their own, for the same reason as the city filter test: in a shared
        // database the Toronto default lets other fixtures push these past the first page.
        var cheap = await factory.CreateListableCaregiverAsync(
            hourlyRate: 18m, yearsOfExperience: 1, city: "Sudbury", postalCode: "P3A 1A1",
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var pricey = await factory.CreateListableCaregiverAsync(
            hourlyRate: 60m, yearsOfExperience: 20, city: "Sudbury", postalCode: "P3A 1A1",
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        // Experience is a paid filter; rate is not. One client covers both.
        var client = await SubscribedRequesterAsync();

        var byRate = await SearchAsync(client, "?city=Sudbury&maxHourlyRate=25");
        Assert.Contains(byRate.Results, r => r.CaregiverProfileId == cheap);
        Assert.DoesNotContain(byRate.Results, r => r.CaregiverProfileId == pricey);

        var byExperience = await SearchAsync(client, "?city=Sudbury&minYearsOfExperience=10");
        Assert.Contains(byExperience.Results, r => r.CaregiverProfileId == pricey);
        Assert.DoesNotContain(byExperience.Results, r => r.CaregiverProfileId == cheap);
    }

    [Fact]
    public async Task The_city_filter_is_applied_case_insensitively()
    {
        // A city no other fixture uses. Every other caregiver defaults to Toronto, so in a shared
        // database this one could fall past the first page and fail for reasons that have nothing
        // to do with case.
        var toronto = await factory.CreateListableCaregiverAsync(
            city: "Kingston", postalCode: "K7L 3N6",
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var ottawa = await factory.CreateListableCaregiverAsync(
            city: "Ottawa", postalCode: "K1P 1J1",
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();
        var results = await SearchAsync(client, "?city=kingston");

        Assert.Contains(results.Results, r => r.CaregiverProfileId == toronto);
        Assert.DoesNotContain(results.Results, r => r.CaregiverProfileId == ottawa);
    }

    [Fact]
    public async Task Availability_filtering_prefers_caregivers_who_cover_the_window()
    {
        var covers = await factory.CreateListableCaregiverAsync(
            availability: [(DayOfWeek.Tuesday, "08:00:00", "20:00:00")],
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var doesNotCover = await factory.CreateListableCaregiverAsync(
            availability: [(DayOfWeek.Tuesday, "08:00:00", "10:00:00")],
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();
        var results = await SearchAsync(
            client, "?day=Tuesday&startTime=13:00:00&endTime=17:00:00");

        var ids = results.Results.Select(r => r.CaregiverProfileId).ToList();
        Assert.Contains(covers, ids);
        // The caregiver whose schedule does not cover the window ranks below the one who does.
        Assert.True(ids.IndexOf(covers) < ids.IndexOf(doesNotCover) || !ids.Contains(doesNotCover));
    }

    [Fact]
    public async Task A_time_window_without_a_day_is_rejected()
    {
        var client = await RequesterAsync();

        var response = await client.GetAsync(
            "/api/v1/caregivers/search?startTime=09:00:00&endTime=17:00:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// An end before the start searches across midnight. Refusing it — which this test
    /// used to assert — hid exactly the caregivers a family looking for overnight care
    /// needs to find.
    /// </summary>
    [Fact]
    public async Task An_end_time_before_the_start_searches_overnight()
    {
        var client = await RequesterAsync();

        var response = await client.GetAsync(
            "/api/v1/caregivers/search?day=Friday&startTime=20:00:00&endTime=07:00:00");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_end_time_equal_to_the_start_is_rejected()
    {
        var client = await RequesterAsync();

        var response = await client.GetAsync(
            "/api/v1/caregivers/search?day=Monday&startTime=09:00:00&endTime=09:00:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Distance_is_reported_once_both_addresses_are_geocoded()
    {
        // Toronto City Hall and the CN Tower — roughly 1.3 km apart.
        await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"],
            latitude: 43.6426, longitude: -79.3871);

        var client = await RequesterAsync();
        await factory.SetRequesterCoordinatesAsync(client, 43.6534, -79.3841);

        var results = await SearchAsync(client);
        var result = results.Results.First(r => r.DistanceKm is not null);

        Assert.InRange(result.DistanceKm!.Value, 1.0, 1.6);
        Assert.False(results.DistanceUnavailable);
    }

    /// <summary>
    /// Without a geocoding provider most addresses have no coordinates. Search must
    /// still work, and must say that distance ranking did not apply.
    /// </summary>
    [Fact]
    public async Task Search_reports_when_distance_ranking_is_unavailable()
    {
        await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);
        var client = await RequesterAsync();

        var results = await SearchAsync(client);

        Assert.True(results.DistanceUnavailable);
        Assert.All(results.Results, r => Assert.Null(r.DistanceKm));
    }

    /// <summary>
    /// Someone who has given an address must not be told to give one.
    ///
    /// The flag alone could not tell "you have no address" from "we could not place the
    /// address you gave", so the screen said "add an address to your profile" to a
    /// requester whose profile already had one — sending them to hunt for a field that
    /// was already filled in. The reason is what makes the difference sayable.
    /// </summary>
    [Fact]
    public async Task An_unlocatable_address_is_reported_as_ours_to_fix_not_theirs()
    {
        var city = $"Ungeocoded{Guid.NewGuid():N}"[..20];

        await factory.CreateListableCaregiverAsync(
            city: city, serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        // A profile with an address, which CreateCareRequesterProfileAsync saves — and
        // no coordinates, because no geocoding provider is configured in tests.
        var client = await RequesterAsync();

        var results = await SearchAsync(client, $"?city={city}");

        Assert.True(results.DistanceUnavailable);
        Assert.Equal("AddressNotLocated", results.DistanceUnavailableReason);
    }

    /// <summary>The one case that is genuinely the reader's to fix.</summary>
    [Fact]
    public async Task No_profile_at_all_is_reported_as_a_missing_address()
    {
        var city = $"Noprofile{Guid.NewGuid():N}"[..20];

        await factory.CreateListableCaregiverAsync(
            city: city, serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        // Registered, but no care-requester profile — so no address either.
        var client = await factory.RegisterCareRequesterAsync();

        var results = await SearchAsync(client, $"?city={city}");

        Assert.Equal("NoAddress", results.DistanceUnavailableReason);
    }

    /// <summary>
    /// Placed reader, unplaced caregivers. Also not theirs to fix, and a different
    /// sentence from either of the other two.
    /// </summary>
    [Fact]
    public async Task Unlocatable_caregivers_are_reported_separately()
    {
        var city = $"Nocaregivers{Guid.NewGuid():N}"[..20];

        await factory.CreateListableCaregiverAsync(
            city: city, serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"]);

        var client = await RequesterAsync();
        await factory.SetRequesterCoordinatesAsync(client, 43.6534, -79.3841);

        var results = await SearchAsync(client, $"?city={city}");

        Assert.Equal("CaregiversNotLocated", results.DistanceUnavailableReason);
    }

    /// <summary>With distance working there is nothing to explain.</summary>
    [Fact]
    public async Task A_working_distance_ranking_reports_no_reason()
    {
        var city = $"Located{Guid.NewGuid():N}"[..20];

        await factory.CreateListableCaregiverAsync(
            city: city, serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"],
            latitude: 43.6426, longitude: -79.3871);

        var client = await RequesterAsync();
        await factory.SetRequesterCoordinatesAsync(client, 43.6534, -79.3841);

        var results = await SearchAsync(client, $"?city={city}");

        Assert.False(results.DistanceUnavailable);
        Assert.Null(results.DistanceUnavailableReason);
    }

    [Fact]
    public async Task Paging_reports_the_total_and_never_repeats_a_caregiver()
    {
        for (var i = 0; i < 3; i++)
        {
            await factory.CreateListableCaregiverAsync(
                serviceCodes: ["SENIOR_COMPANIONSHIP"], certificationCodes: ["POLICE_CHECK"]);
        }

        var client = await RequesterAsync();

        var first = await SearchAsync(client, "?serviceCode=SENIOR_COMPANIONSHIP&page=1&pageSize=2");
        var second = await SearchAsync(client, "?serviceCode=SENIOR_COMPANIONSHIP&page=2&pageSize=2");

        Assert.Equal(2, first.Results.Count);
        Assert.True(first.TotalMatches >= 3);
        Assert.Empty(first.Results
            .Select(r => r.CaregiverProfileId)
            .Intersect(second.Results.Select(r => r.CaregiverProfileId)));
    }

    [Fact]
    public async Task An_oversized_page_is_rejected()
    {
        var client = await RequesterAsync();

        var response = await client.GetAsync("/api/v1/caregivers/search?pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_caregiver_cannot_search_for_caregivers()
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        var response = await caregiver.GetAsync("/api/v1/caregivers/search");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/caregivers/search");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
