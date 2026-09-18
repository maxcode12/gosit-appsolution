using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class ApiSmokeTests(GoSitApiFactory factory)
{
    private sealed record MetaResponse(string Service, string Version, string Environment);

    [Fact]
    public async Task Meta_endpoint_returns_service_identity()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/meta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MetaResponse>();
        Assert.NotNull(body);
        Assert.Equal("gosit-api", body.Service);
    }

    [Fact]
    public async Task Liveness_endpoint_is_healthy_without_dependencies()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_endpoint_reports_the_database()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_echo_correlation_id()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-correlation-123");

        var response = await client.GetAsync("/api/v1/meta");

        Assert.Equal("test-correlation-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
