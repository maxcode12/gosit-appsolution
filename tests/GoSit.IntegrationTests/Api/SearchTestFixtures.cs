using System.Net.Http.Json;
using GoSit.Domain.Caregivers;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Builds caregivers that are actually discoverable.
///
/// Verification is deliberately not reachable through the API — only Trust and Safety
/// may grant it — so these helpers set it directly in the database, which is what the
/// Phase 7 admin endpoint will eventually do.
/// </summary>
public static class SearchTestFixtures
{
    public sealed record SearchBody(
        IReadOnlyList<SearchResultBody> Results,
        int TotalMatches,
        int Page,
        int PageSize,
        bool DistanceUnavailable,
        string? DistanceUnavailableReason);

    public sealed record SearchResultBody(
        Guid CaregiverProfileId,
        string FirstName,
        string LastInitial,
        string? Bio,
        int YearsOfExperience,
        decimal HourlyRate,
        string ApproximateLocation,
        double? DistanceKm,
        decimal? AverageRating,
        int ReviewCount,
        bool IsVerified,
        IReadOnlyList<string> ServiceCodes,
        IReadOnlyList<string> CertificationCodes,
        bool IsFavorite);

    public sealed record DetailBody(
        Guid CaregiverProfileId,
        string FirstName,
        string LastInitial,
        string? Bio,
        int YearsOfExperience,
        decimal HourlyRate,
        string ApproximateLocation,
        int ServiceRadiusKm,
        decimal? AverageRating,
        int ReviewCount,
        bool IsVerified,
        IReadOnlyList<ServiceBody> Services,
        IReadOnlyList<string> CertificationCodes,
        IReadOnlyList<WindowBody> Availability,
        bool IsFavorite);

    public sealed record ServiceBody(string Code, string Name, string CategoryCode);

    public sealed record WindowBody(string Day, TimeOnly StartTime, TimeOnly EndTime);

    public sealed record CaregiverBody(Guid Id, IReadOnlyList<string> ServiceCodes);

    /// <summary>
    /// Registers a caregiver, fills in their profile, services, availability and
    /// certifications, then verifies and lists them.
    /// </summary>
    public static async Task<Guid> CreateListableCaregiverAsync(
        this GoSitApiFactory factory,
        string city = "Toronto",
        string postalCode = "M5S 1M2",
        decimal hourlyRate = 25m,
        int yearsOfExperience = 5,
        string[]? serviceCodes = null,
        string[]? certificationCodes = null,
        (DayOfWeek Day, string Start, string End)[]? availability = null,
        decimal? rating = null,
        int reviewCount = 0,
        double? latitude = null,
        double? longitude = null)
    {
        var client = await factory.RegisterCaregiverAsync();

        var profileResponse = await client.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Experienced with children and seniors.",
            yearsOfExperience,
            hourlyRate,
            address = new
            {
                line1 = "12 Bloor St W",
                line2 = (string?)null,
                city,
                province = "ON",
                postalCode,
                country = "CA",
            },
            serviceRadiusKm = 25,
        });
        profileResponse.EnsureSuccessStatusCode();
        var profile = await profileResponse.Content.ReadFromJsonAsync<CaregiverBody>();

        if (serviceCodes is { Length: > 0 })
        {
            var response = await client.PutAsJsonAsync(
                "/api/v1/caregivers/me/services", new { serviceCodes });
            response.EnsureSuccessStatusCode();
        }

        foreach (var code in certificationCodes ?? [])
        {
            var response = await client.PostAsJsonAsync("/api/v1/caregivers/me/certifications", new
            {
                certificationCode = code,
                issuedOn = "2026-01-10",
                expiresOn = "2028-01-10",
                documentReference = (string?)null,
            });
            response.EnsureSuccessStatusCode();
        }

        var windows = availability ?? [(DayOfWeek.Monday, "09:00:00", "17:00:00")];
        var availabilityResponse = await client.PutAsJsonAsync("/api/v1/caregivers/me/availability", new
        {
            windows = windows.Select(w => new
            {
                day = w.Day.ToString(),
                startTime = w.Start,
                endTime = w.End,
            }),
        });
        availabilityResponse.EnsureSuccessStatusCode();

        await factory.MakeListableAsync(
            profile!.Id, certificationCodes ?? [], rating, reviewCount, latitude, longitude);

        return profile.Id;
    }

    /// <summary>
    /// Approves the caregiver's certifications, marks them verified, and sets them
    /// accepting work — the state Trust and Safety will produce in Phase 7.
    /// </summary>
    private static async Task MakeListableAsync(
        this GoSitApiFactory factory,
        Guid caregiverProfileId,
        IReadOnlyCollection<string> certificationCodes,
        decimal? rating,
        int reviewCount,
        double? latitude,
        double? longitude)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var profile = await context.CaregiverProfiles
            .Include(p => p.Certifications)
            .ThenInclude(c => c.Certification)
            .SingleAsync(p => p.Id == caregiverProfileId);

        var utcNow = DateTime.UtcNow;

        foreach (var certification in profile.Certifications
                     .Where(c => certificationCodes.Contains(c.Certification.Code))
                     .ToList())
        {
            profile.AttachCertificationDocument(certification.Id, "test/document", utcNow);
            profile.ApproveCertification(certification.Id, utcNow);
        }

        profile.MarkVerified(clearedForPeopleCare: true, utcNow);
        profile.AcceptServiceAgreement("Test Caregiver", utcNow);
        profile.SetAcceptingWork(true, utcNow);

        if (rating is not null || reviewCount > 0)
        {
            profile.UpdateRatingSummary(rating, reviewCount, utcNow);
        }

        if (latitude is { } lat && longitude is { } lon)
        {
            profile.SetServiceArea(profile.Address!.WithCoordinates(lat, lon), profile.ServiceRadiusKm, utcNow);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Geocodes a care requester's saved address so distance ranking can run.</summary>
    public static async Task SetRequesterCoordinatesAsync(
        this GoSitApiFactory factory,
        HttpClient requesterClient,
        double latitude,
        double longitude)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var profileId = (await requesterClient
            .GetFromJsonAsync<CaregiverBody>("/api/v1/care-requesters/me"))!.Id;

        var profile = await context.CareRequesterProfiles.SingleAsync(p => p.Id == profileId);
        profile.SetAddress(profile.Address!.WithCoordinates(latitude, longitude), DateTime.UtcNow);

        await context.SaveChangesAsync();
    }
}
