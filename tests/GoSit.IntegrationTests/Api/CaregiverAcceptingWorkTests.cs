using System.Net;
using System.Net.Http.Json;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Opening and closing a caregiver to new work.
///
/// These deliberately drive the HTTP API alone. The search fixtures reach into the
/// domain to make a caregiver listable, which is convenient but hid a real defect: for
/// a while there was no endpoint that could set <c>AcceptingWork</c> at all, so no
/// caregiver could ever become listable through the product itself. A test that skips
/// the transport cannot catch that.
/// </summary>
[Collection(nameof(ApiCollection))]
public class CaregiverAcceptingWorkTests(GoSitApiFactory factory)
{
    private static object ProfilePayload() => new
    {
        bio = "Ten years with children and seniors.",
        yearsOfExperience = 10,
        hourlyRate = 28m,
        address = new
        {
            line1 = "12 Bloor St W",
            line2 = (string?)null,
            city = "Toronto",
            province = "ON",
            postalCode = "M5S1M2",
            country = "CA",
        },
        serviceRadiusKm = 20,
    };

    private static object AvailabilityPayload() => new
    {
        windows = new[]
        {
            new { day = "Monday", startTime = "08:00:00", endTime = "20:00:00" },
        },
    };

    /// <summary>Registers a caregiver with a complete profile and availability.</summary>
    private async Task<(HttpClient Client, Guid ProfileId)> CaregiverWithProfileAsync()
    {
        var client = await factory.RegisterCaregiverAsync();

        var profile = await client.PutAsJsonAsync("/api/v1/caregivers/me", ProfilePayload());
        profile.EnsureSuccessStatusCode();

        var body = await profile.Content.ReadFromJsonAsync<ProfileBody>();

        (await client.PutAsJsonAsync("/api/v1/caregivers/me/availability", AvailabilityPayload()))
            .EnsureSuccessStatusCode();

        // Listability also requires a signed Caregiver Service Agreement — see
        // CaregiverProfile.IsListable. Signed here so every scenario in this file starts
        // from a caregiver who is missing only what that scenario is actually testing.
        (await client.PostAsJsonAsync(
            "/api/v1/caregivers/me/service-agreement/accept", new { signedName = "Test Caregiver" }))
            .EnsureSuccessStatusCode();

        return (client, body!.Id);
    }

    /// <summary>Marks the profile verified, as Trust and Safety would.</summary>
    private async Task VerifyAsync(Guid caregiverProfileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var profile = await context.CaregiverProfiles.SingleAsync(p => p.Id == caregiverProfileId);
        profile.MarkVerified(clearedForPeopleCare: true, DateTime.UtcNow);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The whole point of the endpoint: a verified caregiver with a complete profile can
    /// make themselves listable without anyone touching the database.
    /// </summary>
    [Fact]
    public async Task A_verified_caregiver_can_open_themselves_to_work_and_become_listable()
    {
        var (client, profileId) = await CaregiverWithProfileAsync();
        await VerifyAsync(profileId);

        var response = await client.PutAsJsonAsync(
            "/api/v1/caregivers/me/accepting-work", new { acceptingWork = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ProfileBody>();

        Assert.True(body!.AcceptingWork);
        Assert.True(body.IsListable);
    }

    /// <summary>
    /// Verification is the floor for being visible to families. An unverified caregiver
    /// must not be able to open themselves to work by calling the endpoint directly.
    /// </summary>
    [Fact]
    public async Task An_unverified_caregiver_cannot_open_themselves_to_work()
    {
        var (client, _) = await CaregiverWithProfileAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/caregivers/me/accepting-work", new { acceptingWork = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var profile = await client.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");

        Assert.False(profile!.AcceptingWork);
        Assert.False(profile.IsListable);
    }

    /// <summary>
    /// Stopping is always allowed. Someone who needs to stop taking work — illness, a
    /// family emergency — must never have to satisfy a condition first.
    /// </summary>
    [Fact]
    public async Task Closing_to_work_is_always_allowed_and_delists_immediately()
    {
        var (client, profileId) = await CaregiverWithProfileAsync();
        await VerifyAsync(profileId);

        (await client.PutAsJsonAsync(
            "/api/v1/caregivers/me/accepting-work", new { acceptingWork = true }))
            .EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync(
            "/api/v1/caregivers/me/accepting-work", new { acceptingWork = false });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ProfileBody>();

        Assert.False(body!.AcceptingWork);
        Assert.False(body.IsListable);
    }

    [Fact]
    public async Task A_care_requester_cannot_use_the_caregiver_endpoint()
    {
        var client = await factory.RegisterCareRequesterAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/caregivers/me/accepting-work", new { acceptingWork = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record ProfileBody(
        Guid Id,
        string? Bio,
        int YearsOfExperience,
        decimal HourlyRate,
        object? Address,
        int ServiceRadiusKm,
        string VerificationStatus,
        bool AcceptingWork,
        bool IsListable);
}
