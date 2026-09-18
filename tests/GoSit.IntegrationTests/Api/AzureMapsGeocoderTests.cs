using System.Net;
using System.Text;
using System.Web;
using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// What the geocoder actually puts on the wire, and what it does with the answer.
///
/// The real <see cref="AzureMapsGeocoder"/> is pointed at a local stub speaking Azure
/// Maps' wire format. Substituting at the <see cref="IGeocoder"/> seam would prove Go
/// Sit's own logic but never run a line of the client — and the mistakes that matter
/// here are all in the client: a credential in the wrong place, a country filter that
/// never gets sent, a low-confidence guess stored as fact.
/// </summary>
public sealed class AzureMapsStub : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Task _loop;

    public sealed record Captured(string Path, string Query, string? SubscriptionKeyHeader);

    public List<Captured> Requests { get; } = [];

    /// <summary>The JSON to answer with. Defaults to a confident downtown Toronto match.</summary>
    public string Response { get; set; } =
        """{"results":[{"score":0.98,"position":{"lat":43.6534,"lon":-79.3841}}]}""";

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public string BaseUrl { get; }

    public AzureMapsStub()
    {
        BaseUrl = LoopbackPort.StartOnFreePort(_listener);

        _loop = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            Requests.Add(new Captured(
                context.Request.Url?.AbsolutePath ?? string.Empty,
                context.Request.Url?.Query ?? string.Empty,
                context.Request.Headers["Subscription-Key"]));

            var payload = Encoding.UTF8.GetBytes(Response);

            context.Response.StatusCode = (int)Status;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(payload);
            context.Response.Close();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();

        await Task.WhenAny(_loop, Task.Delay(TimeSpan.FromSeconds(2)));
    }
}

public class AzureMapsGeocoderTests
{
    private static readonly GeocodeRequest Address =
        new("100 Queen St W", "Unit 4", "Toronto", "ON", "M5H2N2", "CA");

    private static AzureMapsGeocoder GeocoderFor(
        AzureMapsStub stub,
        double minimumConfidence = 0.5)
    {
        var options = new AzureMapsOptions
        {
            SubscriptionKey = "test-key",
            BaseUrl = stub.BaseUrl,
            MinimumConfidence = minimumConfidence,
        };

        var client = new HttpClient { BaseAddress = new Uri(stub.BaseUrl) };

        return new AzureMapsGeocoder(
            client, Options.Create(options), NullLogger<AzureMapsGeocoder>.Instance);
    }

    [Fact]
    public async Task A_confident_match_is_returned()
    {
        await using var stub = new AzureMapsStub();

        var result = await GeocoderFor(stub).GeocodeAsync(Address, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(43.6534, result.Latitude, 4);
        Assert.Equal(-79.3841, result.Longitude, 4);
    }

    /// <summary>
    /// The credential belongs in a header. In the query string it ends up in request
    /// logs, proxy logs, and any exception that prints the URI.
    /// </summary>
    [Fact]
    public async Task The_subscription_key_travels_in_a_header_not_the_url()
    {
        await using var stub = new AzureMapsStub();

        await GeocoderFor(stub).GeocodeAsync(Address, CancellationToken.None);

        var request = Assert.Single(stub.Requests);

        Assert.Equal("test-key", request.SubscriptionKeyHeader);
        Assert.DoesNotContain("subscription-key", request.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key", request.Query, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without a country filter a Canadian street name will match an American one, and
    /// the caregiver ends up ranked from the wrong continent.
    /// </summary>
    [Fact]
    public async Task The_search_is_restricted_to_one_country()
    {
        await using var stub = new AzureMapsStub();

        await GeocoderFor(stub).GeocodeAsync(Address, CancellationToken.None);

        var request = Assert.Single(stub.Requests);
        var query = HttpUtility.ParseQueryString(request.Query);

        Assert.Equal("/search/address/json", request.Path);
        Assert.Equal("CA", query["countrySet"]);
        Assert.Equal("1.0", query["api-version"]);
    }

    /// <summary>
    /// "Unit 4" does not help a geocoder find a building and regularly makes the match
    /// worse, so it is left out of the query.
    /// </summary>
    [Fact]
    public async Task The_query_omits_the_unit_line_and_spaces_the_postal_code()
    {
        await using var stub = new AzureMapsStub();

        await GeocoderFor(stub).GeocodeAsync(Address, CancellationToken.None);

        var query = HttpUtility.ParseQueryString(Assert.Single(stub.Requests).Query)["query"];

        Assert.NotNull(query);
        Assert.Contains("100 Queen St W", query, StringComparison.Ordinal);
        Assert.Contains("M5H 2N2", query, StringComparison.Ordinal);
        Assert.DoesNotContain("Unit 4", query, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rule that matters most. Azure Maps answers almost anything with its best
    /// guess; storing a guess puts someone kilometres from where they are, in every
    /// search, with no symptom.
    /// </summary>
    [Fact]
    public async Task A_low_confidence_match_is_discarded()
    {
        await using var stub = new AzureMapsStub
        {
            Response = """{"results":[{"score":0.31,"position":{"lat":43.0,"lon":-79.0}}]}""",
        };

        var result = await GeocoderFor(stub).GeocodeAsync(Address, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task The_confidence_floor_is_configurable()
    {
        await using var stub = new AzureMapsStub
        {
            Response = """{"results":[{"score":0.31,"position":{"lat":43.0,"lon":-79.0}}]}""",
        };

        var result = await GeocoderFor(stub, minimumConfidence: 0.2)
            .GeocodeAsync(Address, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task No_match_returns_null()
    {
        await using var stub = new AzureMapsStub { Response = """{"results":[]}""" };

        Assert.Null(await GeocoderFor(stub).GeocodeAsync(Address, CancellationToken.None));
    }

    /// <summary>
    /// A provider error is never allowed to propagate — an address must still save.
    /// </summary>
    [Fact]
    public async Task A_provider_error_returns_null_rather_than_throwing()
    {
        await using var stub = new AzureMapsStub { Status = HttpStatusCode.TooManyRequests };

        Assert.Null(await GeocoderFor(stub).GeocodeAsync(Address, CancellationToken.None));
    }

    [Fact]
    public async Task An_unreachable_provider_returns_null_rather_than_throwing()
    {
        var options = new AzureMapsOptions
        {
            SubscriptionKey = "test-key",
            // A port with nothing listening.
            BaseUrl = "http://127.0.0.1:1",
        };

        var geocoder = new AzureMapsGeocoder(
            new HttpClient { BaseAddress = new Uri(options.BaseUrl) },
            Options.Create(options),
            NullLogger<AzureMapsGeocoder>.Instance);

        Assert.Null(await geocoder.GeocodeAsync(Address, CancellationToken.None));
    }

    /// <summary>
    /// With no key configured, nothing is called at all — no failed request, no log
    /// line on every profile save.
    /// </summary>
    [Fact]
    public async Task With_no_key_configured_the_provider_is_never_called()
    {
        await using var stub = new AzureMapsStub();

        var geocoder = new AzureMapsGeocoder(
            new HttpClient { BaseAddress = new Uri(stub.BaseUrl) },
            Options.Create(new AzureMapsOptions { BaseUrl = stub.BaseUrl }),
            NullLogger<AzureMapsGeocoder>.Instance);

        Assert.False(geocoder.IsLive);
        Assert.Null(await geocoder.GeocodeAsync(Address, CancellationToken.None));
        Assert.Empty(stub.Requests);
    }
}
