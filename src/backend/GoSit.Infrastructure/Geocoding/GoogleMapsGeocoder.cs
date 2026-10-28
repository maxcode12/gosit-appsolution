using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Geocoding;

/// <summary>
/// Geocoding through the Google Maps Geocoding API.
///
/// Two decisions worth knowing about:
///
/// <b>The key is in the query string, because Google leaves no alternative.</b> The
/// Geocoding API rejects the <c>X-Goog-Api-Key</c> header. The registration therefore
/// removes the HttpClient's request logging for this client, so the URL — and the key in
/// it — never reaches a log; nothing here logs the URI either.
///
/// <b>Only precise matches are kept.</b> Google answers almost anything with a best
/// guess. A coordinate stored on an address is read as "this is the house", and arrival
/// check-in compares a caregiver's phone against it. So a result is used only when
/// Google places it on the building (<c>ROOFTOP</c>) or interpolates it along the
/// street (<c>RANGE_INTERPOLATED</c>), and not when it reports a partial match.
/// Anything vaguer is treated as no result at all.
/// </summary>
public sealed class GoogleMapsGeocoder(
    HttpClient client,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleMapsGeocoder> logger) : IGeocoder
{
    private readonly GoogleMapsOptions _options = options.Value;

    public bool IsLive => _options.IsConfigured;

    public async Task<GeoCoordinates?> GeocodeAsync(
        GeocodeRequest address,
        CancellationToken cancellationToken)
    {
        if (!IsLive)
        {
            return null;
        }

        var path =
            $"/maps/api/geocode/json?address={HttpUtility.UrlEncode(Format(address))}"
            + $"&components=country:{_options.Country}"
            + $"&key={HttpUtility.UrlEncode(_options.ApiKey)}";

        try
        {
            using var response = await client.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The address is never logged — it is someone's home.
                logger.LogWarning(
                    "Google Maps returned {StatusCode} for a geocode request",
                    (int)response.StatusCode);

                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GeocodeResponse>(
                cancellationToken);

            if (payload?.Status == "ZERO_RESULTS")
            {
                logger.LogInformation("Google Maps found no match for an address");
                return null;
            }

            if (payload?.Status != "OK")
            {
                // REQUEST_DENIED, OVER_QUERY_LIMIT and the like are a configuration or
                // quota problem, not a bad address — worth seeing in the logs.
                logger.LogWarning(
                    "Google Maps answered {Status} for a geocode request",
                    payload?.Status ?? "(none)");

                return null;
            }

            var best = payload.Results?.FirstOrDefault();

            if (best?.Geometry?.Location is not { } location)
            {
                return null;
            }

            if (best.PartialMatch
                || best.Geometry.LocationType is not ("ROOFTOP" or "RANGE_INTERPOLATED"))
            {
                logger.LogInformation(
                    "Discarding a Google Maps match of type {LocationType}; too vague to store",
                    best.Geometry.LocationType ?? "(none)");

                return null;
            }

            return new GeoCoordinates(location.Lat, location.Lng);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Non-fatal by design: the address still saves, and the backfill sweep will
            // try again. See IGeocoder. The exception message can carry the request URI,
            // so only its type is logged.
            logger.LogWarning(
                "Could not reach Google Maps ({ExceptionType}); the address stays unresolved",
                ex.GetType().Name);

            return null;
        }
    }

    /// <summary>
    /// Builds the free-form query. Line 2 is left out for the same reason as the Azure
    /// Maps geocoder: "Unit 4" does not help find a building.
    /// </summary>
    private static string Format(GeocodeRequest address)
    {
        var postalCode = address.PostalCode.Length == 6
            ? $"{address.PostalCode[..3]} {address.PostalCode[3..]}"
            : address.PostalCode;

        return string.Join(
            ", ",
            new[] { address.Line1, address.City, address.Province, postalCode, address.Country }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private sealed record GeocodeResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("results")] IReadOnlyList<GeocodeResult>? Results);

    private sealed record GeocodeResult(
        [property: JsonPropertyName("partial_match")] bool PartialMatch,
        [property: JsonPropertyName("geometry")] Geometry? Geometry);

    private sealed record Geometry(
        [property: JsonPropertyName("location_type")] string? LocationType,
        [property: JsonPropertyName("location")] LatLng? Location);

    private sealed record LatLng(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lng")] double Lng);
}
