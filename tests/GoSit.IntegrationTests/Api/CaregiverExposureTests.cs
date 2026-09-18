using System.Net.Http.Json;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Search performance — the half of CAREGIVER_INSIGHTS the plan sold and the product
/// could not measure.
///
/// The counting is straightforward. What these mostly guard is the shape: one row per
/// caregiver per day, carrying counts and nothing else, so the store cannot answer
/// "which families looked at this caregiver" however it is queried later. On a care
/// marketplace that question is close to "who is looking for help with a child or a
/// parent", and the table is built so it has no answer.
/// </summary>
[Collection(nameof(ApiCollection))]
public class CaregiverExposureTests(GoSitApiFactory factory)
{
    private sealed record InsightsBody(
        int SearchAppearances,
        int ProfileViews,
        double? ViewRate,
        bool SearchPerformanceAvailable);

    private sealed record DetailBody(Guid CaregiverProfileId);

    private async Task<(Guid ProfileId, string City)> ListedCaregiverAsync()
    {
        // A city of its own, so other tests searching in the background cannot add
        // impressions to the caregiver under test.
        var city = $"Seen{Guid.NewGuid():N}"[..20];

        var profileId = await factory.CreateListableCaregiverAsync(
            city: city,
            serviceCodes: ["BABYSITTING"],
            certificationCodes: ["VULNERABLE_SECTOR"]);

        return (profileId, city);
    }

    private static Task<HttpResponseMessage> SearchAsync(HttpClient client, string city) =>
        client.GetAsync($"/api/v1/caregivers/search?city={city}");

    /// <summary>The counts as stored, read straight out of the table.</summary>
    private async Task<(int Rows, int Impressions, int Views)> ExposureAsync(Guid profileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var rows = await context.CaregiverExposures
            .AsNoTracking()
            .Where(e => e.CaregiverProfileId == profileId)
            .ToListAsync();

        return (rows.Count, rows.Sum(e => e.Impressions), rows.Sum(e => e.ProfileViews));
    }

    // ---- counting ----

    [Fact]
    public async Task Appearing_in_a_page_of_results_is_counted()
    {
        var (profileId, city) = await ListedCaregiverAsync();
        var requester = await factory.RegisterCareRequesterAsync();

        (await SearchAsync(requester, city)).EnsureSuccessStatusCode();

        var exposure = await ExposureAsync(profileId);

        Assert.Equal(1, exposure.Impressions);
        Assert.Equal(0, exposure.Views);
    }

    [Fact]
    public async Task Impressions_accumulate_across_searches()
    {
        var (profileId, city) = await ListedCaregiverAsync();
        var requester = await factory.RegisterCareRequesterAsync();

        await SearchAsync(requester, city);
        await SearchAsync(requester, city);
        await SearchAsync(requester, city);

        Assert.Equal(3, (await ExposureAsync(profileId)).Impressions);
    }

    [Fact]
    public async Task Opening_a_profile_is_counted_as_a_view()
    {
        var (profileId, city) = await ListedCaregiverAsync();
        var requester = await factory.RegisterCareRequesterAsync();

        (await requester.GetAsync($"/api/v1/caregivers/{profileId}")).EnsureSuccessStatusCode();

        var exposure = await ExposureAsync(profileId);

        Assert.Equal(1, exposure.Views);
        // Opening a profile is not appearing in search.
        Assert.Equal(0, exposure.Impressions);
    }

    /// <summary>
    /// A caregiver cannot reach the public profile page at all — the search controller is
    /// CareRequesterOnly — so their own visits cannot inflate the view count.
    ///
    /// Written against the refusal rather than against the self-view guard in the
    /// handler, because the guard is unreachable while this rule holds and a test that
    /// exercised it would be testing code that never runs. If the profile page is ever
    /// made public, this test fails and the guard behind it starts doing real work.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_reach_the_profile_page_so_cannot_inflate_views()
    {
        var (profileId, _) = await ListedCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(profileId);

        var response = await caregiver.GetAsync($"/api/v1/caregivers/{profileId}");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, (await ExposureAsync(profileId)).Views);
    }

    // ---- the shape, which is the privacy promise ----

    /// <summary>
    /// The test that matters most here.
    ///
    /// Three different families search; the store still holds one row. If a searcher were
    /// ever recorded — as a column or as a row per viewer — this count would rise with
    /// them, so it fails the moment the table stops being anonymous.
    /// </summary>
    [Fact]
    public async Task Different_families_searching_leave_one_row_not_one_each()
    {
        var (profileId, city) = await ListedCaregiverAsync();

        for (var i = 0; i < 3; i++)
        {
            var requester = await factory.RegisterCareRequesterAsync();
            (await SearchAsync(requester, city)).EnsureSuccessStatusCode();
        }

        var exposure = await ExposureAsync(profileId);

        Assert.Equal(1, exposure.Rows);
        Assert.Equal(3, exposure.Impressions);
    }

    /// <summary>
    /// The same promise, stated against the schema rather than the data: there is nowhere
    /// in this table to put a family even if someone later wanted to.
    /// </summary>
    [Fact]
    public void The_exposure_table_has_no_column_naming_a_searcher()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var columns = context.Model
            .FindEntityType(typeof(Domain.Caregivers.CaregiverExposure))!
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(
            new[] { "CaregiverProfileId", "Id", "Impressions", "OnDate", "ProfileViews" },
            columns.Order());

        Assert.DoesNotContain(columns, name =>
            name.Contains("Requester", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Viewer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("UserId", StringComparison.OrdinalIgnoreCase));
    }

    // ---- what the caregiver is shown ----

    [Fact]
    public async Task Insights_report_appearances_views_and_the_rate()
    {
        var (profileId, city) = await ListedCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(profileId);

        (await caregiver.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode = "PRO_CAREGIVER", promoCode = (string?)null }))
            .EnsureSuccessStatusCode();

        var requester = await factory.RegisterCareRequesterAsync();

        // Shown four times, opened once.
        for (var i = 0; i < 4; i++)
        {
            await SearchAsync(requester, city);
        }

        await requester.GetAsync($"/api/v1/caregivers/{profileId}");

        var insights = await caregiver.GetFromJsonAsync<InsightsBody>(
            "/api/v1/caregivers/me/insights");

        Assert.True(insights!.SearchPerformanceAvailable);
        Assert.Equal(4, insights.SearchAppearances);
        Assert.Equal(1, insights.ProfileViews);
        Assert.Equal(0.25, insights.ViewRate);
    }

    /// <summary>
    /// A caregiver nobody has been shown has no view rate. Reporting 0% would read as a
    /// verdict on a profile that has not been tested — the same rule the booking rates
    /// already follow.
    /// </summary>
    [Fact]
    public async Task A_caregiver_nobody_has_seen_has_no_view_rate()
    {
        var (profileId, _) = await ListedCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(profileId);

        (await caregiver.PostAsJsonAsync(
            "/api/v1/me/subscription", new { planCode = "PRO_CAREGIVER", promoCode = (string?)null }))
            .EnsureSuccessStatusCode();

        var insights = await caregiver.GetFromJsonAsync<InsightsBody>(
            "/api/v1/caregivers/me/insights");

        Assert.Equal(0, insights!.SearchAppearances);
        Assert.Null(insights.ViewRate);
    }
}
