using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container, with the schema
/// created from the actual EF migrations rather than EnsureCreated — so the tests
/// exercise the same DDL that ships.
/// </summary>
public class GoSitApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /*
     * Real blob storage, in a container, rather than a stubbed IDocumentStore.
     *
     * A stub would prove the handler calls the store and nothing about whether a document
     * survives the round trip — and the round trip is the feature. Azurite runs the same
     * client the deployment does.
     */
    /*
     * --skipApiVersionCheck: the pinned Azurite image is older than the storage API
     * version the client speaks, and it refuses anything it does not recognise. The
     * alternative is pinning the client back to whatever this image happens to know,
     * which would mean the tests dictate the protocol production uses.
     */
    private readonly AzuriteContainer _azurite = new AzuriteBuilder()
        .WithCommand("--skipApiVersionCheck")
        .Build();

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _azurite.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        // Guard against the host silently falling back to appsettings and pointing the
        // suite at a developer's real database.
        var actual = context.Database.GetConnectionString();
        if (actual != _postgres.GetConnectionString())
        {
            throw new InvalidOperationException(
                "The test host is not using the test container's database. "
                + "Check the configuration overrides in GoSitApiFactory.");
        }

        // Building the host above already migrated and seeded, because the app does both
        // at startup under the Development environment. These calls are the safety net
        // for that behaviour changing, and are no-ops when it has not: MigrateAsync
        // applies nothing and the seeder finds every row already present.
        await context.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _azurite.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Settings applied after the base ones, so a derived factory can change individual
    /// values (for example, a deliberately tiny rate limit).
    /// </summary>
    protected virtual IDictionary<string, string?> SettingOverrides =>
        new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // UseSetting writes straight into the app's ConfigurationManager, so these win
        // over appsettings.json. Adding a configuration source instead does not reliably
        // take precedence over the files the host has already loaded.
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["DocumentStorage:ConnectionString"] = _azurite.GetConnectionString(),
            // Redis is not needed by the authentication slice; an empty value skips both
            // its registration and its readiness check.
            ["ConnectionStrings:Redis"] = string.Empty,
            ["Jwt:Issuer"] = "https://tests.gosit.local",
            ["Jwt:Audience"] = "gosit-tests",
            ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-long-enough-for-hmac-sha256",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "14",
            // The suite drives far more credential calls from one client than a real user
            // would. Rate limiting is exercised deliberately in RateLimitTests.
            ["RateLimiting:AuthPermitLimit"] = "100000",
            ["RateLimiting:WebhookPermitLimit"] = "100000",
            // No Stripe secret key, so the simulated provider stands in. The webhook
            // secret IS set, because webhook verification is never simulated — the tests
            // exercise the same parser that runs in production.
            ["Stripe:SecretKey"] = string.Empty,
            ["Stripe:WebhookSecret"] = StripeWebhookSigner.TestWebhookSecret,
            ["Stripe:WebhookToleranceSeconds"] = "300",
            ["Payments:CommissionRate"] = "0.15",
            ["RateLimiting:AuthWindowSeconds"] = "60",
            // The contact form is exercised heavily from one client; its throttle gets its own
            // test (EnquiryRateLimitTests), and the allowed website is named explicitly.
            ["RateLimiting:FormPermitLimit"] = "100000",
            ["Cors:PublicFormOrigins"] = "https://www.gosits.test",
            // Sign-up is closed by default everywhere but Development. Spelled out here so
            // the suite does not depend on that file; ClosedRegistrationTests overrides it.
            ["Registration:Open"] = "true",
        };

        foreach (var (key, value) in settings.Concat(SettingOverrides))
        {
            builder.UseSetting(key, value);
        }

        // The video service is the one dependency that cannot run in a container. The fake
        // is registered as itself as well, so a test can read what it was asked to do.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IVideoMeetingProvider>();
            services.RemoveAll<IRecordingStore>();
            services.AddSingleton<FakeVideoMeetingProvider>();
            services.AddSingleton<IVideoMeetingProvider>(sp => sp.GetRequiredService<FakeVideoMeetingProvider>());
            services.AddSingleton<InMemoryRecordingStore>();
            services.AddSingleton<IRecordingStore>(sp => sp.GetRequiredService<InMemoryRecordingStore>());
        });
    }
}
