using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>Runs against its own host configured with a deliberately tiny limit.</summary>
public sealed class RateLimitedApiFactory : GoSitApiFactory
{
    public const int PermitLimit = 3;

    protected override IDictionary<string, string?> SettingOverrides =>
        new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = PermitLimit.ToString(),
        };
}

public class RateLimitTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public RateLimitTests(RateLimitedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Repeated_login_attempts_are_throttled()
    {
        var client = _factory.CreateClient();
        var attempt = new { email = "attacker@example.com", password = "guess-after-guess" };

        // Exhaust the window. These are rejected on credentials, not on rate limit.
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/v1/auth/login", attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/api/v1/auth/login", attempt);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task Rate_limiting_does_not_apply_to_unauthenticated_metadata()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < RateLimitedApiFactory.PermitLimit + 3; i++)
        {
            var response = await client.GetAsync("/api/v1/meta");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
