using System.Net.Http.Json;
using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>A geocoder a test can steer: resolve, fail, or be switched off entirely.</summary>
public sealed class SteerableGeocoder : IGeocoder
{
    public bool IsLive { get; set; } = true;

    /// <summary>Null makes every lookup fail to resolve.</summary>
    public GeoCoordinates? Result { get; set; } = new(43.6534, -79.3841);

    public int Calls { get; private set; }

    public Task<GeoCoordinates?> GeocodeAsync(
        GeocodeRequest address,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(IsLive ? Result : null);
    }
}

public sealed class GeocodingFactoryFixture : GoSitApiFactory
{
    public SteerableGeocoder Geocoder { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IGeocoder>(Geocoder);
        });
    }
}

/// <summary>
/// Resolving an address when a profile is saved.
///
/// The guarantee under test is the awkward one: <b>a geocoding failure must never stop
/// someone saving where they live.</b> It is easy to write this so an outage at a
/// mapping provider makes profile updates fail, and easy not to notice until it happens.
/// </summary>
public class AddressGeocodingTests(GeocodingFactoryFixture factory)
    : IClassFixture<GeocodingFactoryFixture>
{
    private static object CaregiverProfile() => new
    {
        bio = "Ten years with children and seniors.",
        yearsOfExperience = 10,
        hourlyRate = 30m,
        address = new
        {
            line1 = "100 Queen St W",
            line2 = (string?)null,
            city = "Toronto",
            province = "ON",
            postalCode = "M5H2N2",
            country = "CA",
        },
        serviceRadiusKm = 20,
    };

    private async Task<(double? Lat, double? Lon)> CaregiverCoordinatesAsync(Guid profileId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var profile = await context.CaregiverProfiles
            .AsNoTracking()
            .SingleAsync(p => p.Id == profileId);

        return (profile.Address?.Latitude, profile.Address?.Longitude);
    }

    private sealed record ProfileBody(Guid Id);

    [Fact]
    public async Task Saving_a_caregiver_address_resolves_it()
    {
        factory.Geocoder.IsLive = true;
        factory.Geocoder.Result = new GeoCoordinates(43.6534, -79.3841);

        var client = await factory.RegisterCaregiverAsync();

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me", CaregiverProfile());
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ProfileBody>();
        var (lat, lon) = await CaregiverCoordinatesAsync(profile!.Id);

        Assert.Equal(43.6534, lat!.Value, 4);
        Assert.Equal(-79.3841, lon!.Value, 4);
    }

    [Fact]
    public async Task Saving_a_care_requester_address_resolves_it()
    {
        factory.Geocoder.IsLive = true;
        factory.Geocoder.Result = new GeoCoordinates(43.7000, -79.4000);

        var client = await factory.RegisterCareRequesterAsync();

        var response = await client.PutAsJsonAsync("/api/v1/care-requesters/me", new
        {
            phoneNumber = "416 555 0134",
            aboutMe = (string?)null,
            address = new
            {
                line1 = "88 Queen St W",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5H2N2",
                country = "CA",
            },
        });

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        var stored = await context.CareRequesterProfiles
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .FirstAsync();

        Assert.Equal(43.7000, stored.Address!.Latitude!.Value, 4);
    }

    /// <summary>
    /// The important one. A mapping outage must cost distance ranking, not the ability
    /// to record where someone lives.
    /// </summary>
    [Fact]
    public async Task An_address_still_saves_when_geocoding_fails()
    {
        factory.Geocoder.IsLive = true;
        factory.Geocoder.Result = null;

        var client = await factory.RegisterCaregiverAsync();

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me", CaregiverProfile());

        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ProfileBody>();
        var (lat, _) = await CaregiverCoordinatesAsync(profile!.Id);

        Assert.Null(lat);

        // And the address itself is intact.
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        var stored = await context.CaregiverProfiles.AsNoTracking().SingleAsync(p => p.Id == profile.Id);

        Assert.Equal("100 Queen St W", stored.Address!.Line1);
    }

    /// <summary>With no provider, nothing is called on every profile save.</summary>
    [Fact]
    public async Task No_provider_means_no_lookup()
    {
        factory.Geocoder.IsLive = false;

        var before = factory.Geocoder.Calls;

        var client = await factory.RegisterCaregiverAsync();
        (await client.PutAsJsonAsync("/api/v1/caregivers/me", CaregiverProfile()))
            .EnsureSuccessStatusCode();

        factory.Geocoder.IsLive = true;

        Assert.Equal(before, factory.Geocoder.Calls);
    }

    /// <summary>
    /// The backfill exists because a failed geocode has no symptom — an unresolved
    /// caregiver is simply invisible to distance ranking, and nobody is told.
    /// </summary>
    [Fact]
    public async Task The_backfill_resolves_an_address_that_failed_earlier()
    {
        factory.Geocoder.IsLive = true;
        factory.Geocoder.Result = null;

        var client = await factory.RegisterCaregiverAsync();
        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me", CaregiverProfile());
        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<ProfileBody>();

        Assert.Null((await CaregiverCoordinatesAsync(profile!.Id)).Lat);

        // The provider recovers.
        factory.Geocoder.Result = new GeoCoordinates(43.6534, -79.3841);

        await factory.Services
            .GetRequiredService<GeocodingBackfillSweeper>()
            .BackfillAsync(CancellationToken.None);

        var (lat, lon) = await CaregiverCoordinatesAsync(profile.Id);

        Assert.Equal(43.6534, lat!.Value, 4);
        Assert.Equal(-79.3841, lon!.Value, 4);
    }

    [Fact]
    public async Task The_backfill_does_nothing_when_no_provider_is_configured()
    {
        factory.Geocoder.IsLive = false;

        var result = await factory.Services
            .GetRequiredService<GeocodingBackfillSweeper>()
            .BackfillAsync(CancellationToken.None);

        factory.Geocoder.IsLive = true;

        Assert.Equal(0, result.Resolved);
    }
}
