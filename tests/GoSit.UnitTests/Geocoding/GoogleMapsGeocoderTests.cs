using System.Net;
using System.Text;
using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoSit.UnitTests.Geocoding;

public sealed class GoogleMapsGeocoderTests
{
    private static readonly GeocodeRequest Address =
        new("100 Queen St W", null, "Toronto", "ON", "M5H2N2", "CA");

    [Fact]
    public async Task A_rooftop_match_is_used()
    {
        var result = await Geocode("""
            {"status":"OK","results":[{"geometry":{"location_type":"ROOFTOP","location":{"lat":43.65,"lng":-79.38}}}]}
            """);

        Assert.NotNull(result);
        Assert.Equal(43.65, result.Latitude);
        Assert.Equal(-79.38, result.Longitude);
    }

    [Theory]
    [InlineData("""{"status":"OK","results":[{"geometry":{"location_type":"APPROXIMATE","location":{"lat":1,"lng":2}}}]}""")]
    [InlineData("""{"status":"OK","results":[{"geometry":{"location_type":"GEOMETRIC_CENTER","location":{"lat":1,"lng":2}}}]}""")]
    [InlineData("""{"status":"OK","results":[{"partial_match":true,"geometry":{"location_type":"ROOFTOP","location":{"lat":1,"lng":2}}}]}""")]
    [InlineData("""{"status":"ZERO_RESULTS","results":[]}""")]
    [InlineData("""{"status":"REQUEST_DENIED","results":[]}""")]
    public async Task A_vague_or_missing_match_is_discarded(string body)
    {
        Assert.Null(await Geocode(body));
    }

    [Fact]
    public async Task Nothing_is_sent_without_a_key()
    {
        var geocoder = Create(string.Empty, new Handler("{}"));

        Assert.False(geocoder.IsLive);
        Assert.Null(await geocoder.GeocodeAsync(Address, CancellationToken.None));
    }

    private static Task<GeoCoordinates?> Geocode(string body) =>
        Create("test-key", new Handler(body)).GeocodeAsync(Address, CancellationToken.None);

    private static GoogleMapsGeocoder Create(string key, HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://maps.test") },
            Options.Create(new GoogleMapsOptions { ApiKey = key }),
            NullLogger<GoogleMapsGeocoder>.Instance);

    private sealed class Handler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
