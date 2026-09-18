using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Geocoding;

/// <summary>
/// Geocoding through Azure Maps Search.
///
/// Two decisions worth knowing about:
///
/// <b>The key goes in a header, not the query string.</b> Azure Maps accepts
/// <c>?subscription-key=</c>, and every example uses it — but a credential in a URL ends
/// up in request logs, proxy logs, and any exception that prints the URI. The
/// <c>Subscription-Key</c> header does the same job without leaving the secret in five
/// places nobody thinks to redact.
///
/// <b>Low-confidence matches are discarded.</b> Azure Maps answers almost anything with
/// its best guess. Storing a guess would put a caregiver kilometres from where they
/// actually are, in every search, with nothing to indicate anything was wrong — so a
/// result below the configured confidence is treated as no result at all.
/// </summary>
public sealed class AzureMapsGeocoder(
    HttpClient client,
    IOptions<AzureMapsOptions> options,
    ILogger<AzureMapsGeocoder> logger) : IGeocoder
{
    private readonly AzureMapsOptions _options = options.Value;

    public bool IsLive => _options.IsConfigured;

    public async Task<GeoCoordinates?> GeocodeAsync(
        GeocodeRequest address,
        CancellationToken cancellationToken)
    {
        if (!IsLive)
        {
            return null;
        }

        var query = HttpUtility.UrlEncode(Format(address));

        var path =
            $"/search/address/json?api-version=1.0&countrySet={_options.CountrySet}"
            + $"&limit=1&query={query}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Subscription-Key", _options.SubscriptionKey);

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The address is never logged — it is someone's home.
                logger.LogWarning(
                    "Azure Maps returned {StatusCode} for a geocode request",
                    (int)response.StatusCode);

                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<SearchAddressResponse>(
                cancellationToken);

            var best = payload?.Results?.FirstOrDefault();

            if (best?.Position is not { } position)
            {
                logger.LogInformation("Azure Maps found no match for an address");
                return null;
            }

            if (best.Score < _options.MinimumConfidence)
            {
                logger.LogInformation(
                    "Discarding an Azure Maps match scoring {Score:0.00}, below the {Minimum:0.00} floor",
                    best.Score,
                    _options.MinimumConfidence);

                return null;
            }

            return new GeoCoordinates(position.Lat, position.Lon);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Non-fatal by design: the address still saves, and the backfill sweep will
            // try again. See IGeocoder.
            logger.LogWarning(ex, "Could not reach Azure Maps; the address stays unresolved");

            return null;
        }
    }

    /// <summary>
    /// Builds the free-form query.
    ///
    /// Line 2 is deliberately left out. "Unit 4" and "Apt 201" do not help a geocoder
    /// find a building and regularly make the match worse — the street address, city,
    /// province and postal code are what resolve a location.
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

    private sealed record SearchAddressResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<SearchResult>? Results);

    private sealed record SearchResult(
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("position")] Position? Position);

    private sealed record Position(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lon")] double Lon);
}

/// <summary>
/// Stands in when no Azure Maps key is configured — local development and the tests.
///
/// Returns null rather than a plausible-looking coordinate. A fake location would make
/// distance ranking appear to work while ordering results by nothing, which is a harder
/// bug to notice than no ranking at all.
/// </summary>
public sealed class UnconfiguredGeocoder : IGeocoder
{
    public bool IsLive => false;

    public Task<GeoCoordinates?> GeocodeAsync(
        GeocodeRequest address,
        CancellationToken cancellationToken) =>
        Task.FromResult<GeoCoordinates?>(null);
}
