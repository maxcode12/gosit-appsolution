using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GoSit.Api.Authentication;
using GoSit.Api.Middleware;
using GoSit.Api.Realtime;
using GoSit.Application;
using GoSit.Application.Abstractions;
using GoSit.Infrastructure;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // preserveStaticLogger keeps the bootstrap logger as the static Log.Logger and gives
    // the host its own fully-configured logger through DI. Without it, building a second
    // host in one process — which integration tests do — fails on the frozen static logger.
    builder.Host.UseSerilog(
        (context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext(),
        preserveStaticLogger: true);

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
    builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
    builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();
    builder.Services.AddSignalR();

    builder.Services.AddGoSitJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorization(options => options.AddGoSitPolicies());

    // Credential endpoints are the prime target for brute force and enumeration.
    var rateLimits = builder.Configuration.GetSection(RateLimitOptions.SectionName)
        .Get<RateLimitOptions>() ?? new RateLimitOptions();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(RateLimitPolicies.Authentication, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                // Partition by client IP: one abusive caller must not lock out everyone.
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimits.AuthPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimits.AuthWindowSeconds),
                    QueueLimit = 0,
                }));

        // The webhook endpoint is anonymous by necessity — a provider cannot present a
        // GoSit token. Its real protection is signature verification; this only stops
        // an unsigned flood from reaching that check. The limit is generous because
        // providers legitimately burst and retry.
        options.AddPolicy(RateLimitPolicies.Webhook, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimits.WebhookPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimits.WebhookWindowSeconds),
                    QueueLimit = 0,
                }));
    });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Enums cross the API boundary by name ("CareRequester"), not by ordinal, so the
            // contract stays readable and renumbering an enum cannot silently change meaning.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddOpenApi();

    var healthChecks = builder.Services.AddHealthChecks();
    var postgres = builder.Configuration.GetConnectionString("Postgres");
    if (!string.IsNullOrWhiteSpace(postgres))
    {
        healthChecks.AddNpgSql(postgres, name: "postgres", tags: ["ready"]);
    }

    var redis = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redis))
    {
        healthChecks.AddRedis(redis, name: "redis", tags: ["ready"]);
    }

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("gosit-api"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            // Marketplace health counters, derived from domain events.
            .AddMeter(GoSit.Infrastructure.Observability.BusinessMetrics.MeterName));

    var app = builder.Build();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        if (app.Environment.IsDevelopment())
        {
            // Convenience for local runs and integration tests only. Deployed
            // environments apply migrations from the release pipeline before the app
            // starts, so scaled-out instances never race each other on schema changes.
            await scope.ServiceProvider.GetRequiredService<GoSitDbContext>()
                .Database.MigrateAsync();
        }

        // The care-service taxonomy is reference data search cannot run without.
        // Idempotent, so it is safe on every boot and on every instance.
        await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();

        // Provisions the first staff account when one is configured, and does nothing
        // otherwise. Admin accounts cannot be self-registered, so without this there is
        // no way to create one — see AdministratorSeeder for the safeguards.
        await scope.ServiceProvider.GetRequiredService<AdministratorSeeder>().SeedAsync();
    }

    app.UseExceptionHandler();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();

    app.MapOpenApi().AllowAnonymous();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "GoSit API v1");
        });
    }

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Without an endpoint here, the fallback authorization policy would turn every
    // mistyped URL into a 401. The safety net still applies to real endpoints.
    app.MapHub<MessagingHub>("/hubs/messaging");

    app.MapFallback(() => Results.NotFound()).AllowAnonymous();
    app.MapHealthChecks("/health/live", new()
    {
        Predicate = _ => false, // liveness: process is up, no dependency checks
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new()
    {
        Predicate = check => check.Tags.Contains("ready"),
    }).AllowAnonymous();

    app.Run();
}
catch (HostAbortedException)
{
    // Raised by the EF Core design-time tools, which build the host and then abort it.
    // Not a failure — rethrow without logging it as one.
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "GoSit API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Named rate-limit policies applied with <see cref="EnableRateLimitingAttribute"/>.</summary>
public static class RateLimitPolicies
{
    public const string Authentication = "authentication";

    /// <summary>Guards the provider webhook endpoint, which is necessarily anonymous.</summary>
    public const string Webhook = "webhook";
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
