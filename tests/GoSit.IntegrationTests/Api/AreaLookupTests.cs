using System.Net;
using System.Net.Http.Json;
using GoSit.Infrastructure.Geocoding;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.SearchTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// The free, bundled Canadian postal-area data: what it knows, and the public "caregivers
/// near you" lookup built on it.
///
/// The lookup is anonymous, so the tests that matter most are about what it will not say:
/// it returns a count and a place, never a person.
/// </summary>
public class PostalAreaDirectoryTests
{
    private static readonly PostalAreaDirectory Directory = new();

    [Fact]
    public void Holds_the_whole_country()
    {
        Assert.InRange(Directory.Count, 1500, 2000);
    }

    [Theory]
    [InlineData("M5V 3L9")]
    [InlineData("m5v3l9")]
    [InlineData("  M5V  ")]
    [InlineData("M5V")]
    public void Finds_a_postal_code_however_it_is_typed(string typed)
    {
        var area = Directory.Find(typed);

        Assert.NotNull(area);
        Assert.Equal("M5V", area.Code);
        Assert.Equal("ON", area.Province);
        Assert.InRange(area.Latitude, 43.0, 44.5);
        Assert.InRange(area.Longitude, -80.0, -79.0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("12345")]
    [InlineData("90210")]
    [InlineData("M")]
    [InlineData("MM5")]
    [InlineData("Z9Z 9Z9")]
    public void Refuses_what_is_not_a_canadian_postal_code(string typed)
    {
        Assert.Null(Directory.Find(typed));
    }

    [Fact]
    public void Suggests_by_postal_prefix()
    {
        var suggestions = Directory.Suggest("M5", 8);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.StartsWith("M5", s.Code));
    }

    [Fact]
    public void Suggests_by_place_name_with_a_leading_match_first()
    {
        var suggestions = Directory.Suggest("toronto", 8);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.Contains("toronto", s.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Suggestions_are_capped()
    {
        Assert.True(Directory.Suggest("a", 5).Count <= 5);
        Assert.Empty(Directory.Suggest("anything", 0));
    }

    [Fact]
    public void Finds_every_area_within_a_radius_of_a_point()
    {
        var toronto = Directory.Find("M5V")!;

        var near = Directory.Within(toronto.Latitude, toronto.Longitude, 10);
        var far = Directory.Within(toronto.Latitude, toronto.Longitude, 100);

        Assert.Contains(near, a => a.Code == "M5V");
        Assert.DoesNotContain(near, a => a.Code == "A1C");
        Assert.True(far.Count >= near.Count);
    }
}

[Collection(nameof(ApiCollection))]
public class AreaLookupTests(GoSitApiFactory factory)
{
    private const string AllowedOrigin = "https://www.gosits.test";

    private sealed record Overview(
        Area Area, int RadiusKm, int CaregiverCount, IReadOnlyList<CategoryCountBody> Categories);

    private sealed record Area(string Code, string Name, string Province);

    private sealed record CategoryCountBody(string Code, string Name, int Count);

    private sealed record Suggestions(IReadOnlyList<Area> Areas);

    private async Task<Overview> OverviewAsync(string query, int? radiusKm = null)
    {
        var client = factory.CreateClient();
        var url = $"/api/v1/areas/overview?query={Uri.EscapeDataString(query)}"
            + (radiusKm is null ? string.Empty : $"&radiusKm={radiusKm}");

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Overview>())!;
    }

    [Fact]
    public async Task Anyone_can_ask_how_many_caregivers_are_near_a_postal_code()
    {
        var overview = await OverviewAsync("V6B 1A1");

        Assert.Equal("V6B", overview.Area.Code);
        Assert.Equal("BC", overview.Area.Province);
        Assert.Equal(25, overview.RadiusKm);
        Assert.True(overview.CaregiverCount >= 0);
    }

    [Fact]
    public async Task Counts_the_caregivers_inside_the_radius_and_not_the_ones_outside_it()
    {
        var before = (await OverviewAsync("V6B")).CaregiverCount;

        // Two in Vancouver, one in St. John's, which is nowhere near.
        await factory.CreateListableCaregiverAsync(
            city: "Vancouver", postalCode: "V6B 1A1", serviceCodes: ["BABYSITTING"]);
        await factory.CreateListableCaregiverAsync(
            city: "Vancouver", postalCode: "V6B 2B2", serviceCodes: ["BABYSITTING"]);
        await factory.CreateListableCaregiverAsync(
            city: "St. John's", postalCode: "A1C 5S7", serviceCodes: ["BABYSITTING"]);

        var after = await OverviewAsync("V6B");

        Assert.Equal(before + 2, after.CaregiverCount);
        Assert.Contains(after.Categories, c => c.Count >= 2);
    }

    [Fact]
    public async Task A_wider_radius_never_finds_fewer()
    {
        var narrow = (await OverviewAsync("M5V", 5)).CaregiverCount;
        var wide = (await OverviewAsync("M5V", 100)).CaregiverCount;

        Assert.True(wide >= narrow);
    }

    [Fact]
    public async Task A_place_name_works_as_well_as_a_postal_code()
    {
        var overview = await OverviewAsync("Toronto");

        Assert.Equal("ON", overview.Area.Province);
    }

    [Fact]
    public async Task Never_reveals_a_person()
    {
        await factory.CreateListableCaregiverAsync(
            city: "Toronto", postalCode: "M5V 3L9", serviceCodes: ["BABYSITTING"]);

        var client = factory.CreateClient();
        var json = await (await client.GetAsync("/api/v1/areas/overview?query=M5V")).Content.ReadAsStringAsync();

        foreach (var forbidden in new[] { "firstName", "lastName", "email", "bio", "line1", "hourlyRate", "caregiverProfileId" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("Z9Z 9Z9")]
    [InlineData("Atlantis")]
    public async Task An_unknown_place_is_not_found(string query)
    {
        var response = await factory.CreateClient()
            .GetAsync($"/api/v1/areas/overview?query={Uri.EscapeDataString(query)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/areas/overview")]
    [InlineData("/api/v1/areas/overview?query=")]
    [InlineData("/api/v1/areas/overview?query=M5V&radiusKm=0")]
    [InlineData("/api/v1/areas/overview?query=M5V&radiusKm=5000")]
    [InlineData("/api/v1/areas/suggest")]
    [InlineData("/api/v1/areas/suggest?q=a")]
    public async Task Bad_input_is_a_validation_error_not_a_crash(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Suggestions_come_back_for_typed_text()
    {
        var client = factory.CreateClient();

        var suggestions = await client.GetFromJsonAsync<Suggestions>("/api/v1/areas/suggest?q=M5");

        Assert.NotNull(suggestions);
        Assert.NotEmpty(suggestions.Areas);
        Assert.True(suggestions.Areas.Count <= 8);
    }

    [Fact]
    public async Task Only_the_companys_own_website_may_read_it_from_a_browser()
    {
        var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/v1/areas/overview?query=M5V");
        allowed.Headers.Add("Origin", AllowedOrigin);

        var stranger = new HttpRequestMessage(HttpMethod.Get, "/api/v1/areas/overview?query=M5V");
        stranger.Headers.Add("Origin", "https://evil.example");

        var client = factory.CreateClient();
        var allowedResponse = await client.SendAsync(allowed);
        var strangerResponse = await client.SendAsync(stranger);

        Assert.True(allowedResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(strangerResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Search_measures_from_the_postal_code_the_reader_typed()
    {
        var near = await factory.CreateListableCaregiverAsync(
            city: "Vancouver", postalCode: "V6B 1A1", serviceCodes: ["BABYSITTING"]);
        await factory.CreateListableCaregiverAsync(
            city: "St. John's", postalCode: "A1C 5S7", serviceCodes: ["BABYSITTING"]);

        var (requester, _) = await factory.RegisterRequesterAsync();
        var response = await requester.GetAsync("/api/v1/caregivers/search?postalCode=V6B&radiusKm=25");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(near.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("St. John", body);
        Assert.DoesNotContain("\"distanceUnavailable\":true", body);
    }
}
