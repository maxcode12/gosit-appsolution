using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoSit.Infrastructure.Scheduling;

public sealed record GeocodingBackfillResult(int Resolved, int StillUnresolved);

/// <summary>
/// Resolves addresses that were saved without coordinates.
///
/// Geocoding is attempted inline when an address is saved, and deliberately allowed to
/// fail — a mapping outage must not stop someone saving where they live. But that leaves
/// a failure mode with no symptom: an unresolved caregiver simply never appears in
/// distance-ranked results, and nobody is told. Nothing in the product would surface it.
///
/// So this picks them up afterwards. It also covers the migration case — every address
/// stored before Azure Maps was configured.
///
/// Hourly, not minutely: an address is not urgent, and a provider that just failed is
/// unlikely to succeed thirty seconds later.
/// </summary>
public sealed class GeocodingBackfillSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<GeocodingBackfillSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    /// <summary>Small: this calls a metered third-party API, and there is no rush.</summary>
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BackfillAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The geocoding backfill failed and will retry");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Runs one cycle. Public so a test can drive a deterministic backfill.</summary>
    public async Task<GeocodingBackfillResult> BackfillAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var geocoder = provider.GetRequiredService<IGeocoder>();

        if (!geocoder.IsLive)
        {
            return new GeocodingBackfillResult(0, 0);
        }

        var context = provider.GetRequiredService<GoSitDbContext>();

        var resolved = 0;
        var unresolved = 0;

        // Caregivers first: an unresolved caregiver is invisible to distance ranking,
        // which costs them work. An unresolved requester only loses their own sorting.
        var caregivers = await context.CaregiverProfiles
            .Where(p => p.Address != null && p.Address.Latitude == null)
            .OrderBy(p => p.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var profile in caregivers)
        {
            var coordinates = await Resolve(geocoder, profile.Address!, cancellationToken);

            if (coordinates is null)
            {
                unresolved++;
                continue;
            }

            profile.SetServiceArea(
                profile.Address!.WithCoordinates(coordinates.Latitude, coordinates.Longitude),
                profile.ServiceRadiusKm,
                DateTime.UtcNow);

            resolved++;
        }

        var requesters = await context.CareRequesterProfiles
            .Where(p => p.Address != null && p.Address.Latitude == null)
            .OrderBy(p => p.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var profile in requesters)
        {
            var coordinates = await Resolve(geocoder, profile.Address!, cancellationToken);

            if (coordinates is null)
            {
                unresolved++;
                continue;
            }

            profile.SetAddress(
                profile.Address!.WithCoordinates(coordinates.Latitude, coordinates.Longitude),
                DateTime.UtcNow);

            resolved++;
        }

        if (resolved > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        if (resolved > 0 || unresolved > 0)
        {
            logger.LogInformation(
                "Geocoding backfill resolved {Resolved}, {Unresolved} still unresolved",
                resolved,
                unresolved);
        }

        return new GeocodingBackfillResult(resolved, unresolved);
    }

    private static Task<GeoCoordinates?> Resolve(
        IGeocoder geocoder,
        Domain.Common.ValueObjects.Address address,
        CancellationToken cancellationToken) =>
        geocoder.GeocodeAsync(
            new GeocodeRequest(
                address.Line1,
                address.Line2,
                address.City,
                address.Province,
                address.PostalCode,
                address.Country),
            cancellationToken);
}
