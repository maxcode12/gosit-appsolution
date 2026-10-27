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

        // The public contact form takes anonymous input from anywhere.
        options.AddPolicy(RateLimitPolicies.PublicForm, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimits.FormPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimits.FormWindowSeconds),
                    QueueLimit = 0,
                }));

        // The public "caregivers near you" lookups.
        options.AddPolicy(RateLimitPolicies.PublicLookup, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimits.LookupPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimits.LookupWindowSeconds),
                    QueueLimit = 0,
                }));
    });

    // Cross-site permission is granted to named websites and to one policy only. The web
    // app calls the API from its own server, and the mobile apps are not browsers, so
    // nothing else needs it. With no origins configured, no website is allowed.
    var formOrigins = (builder.Configuration["Cors:PublicFormOrigins"] ?? string.Empty)
        .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicies.PublicForms, policy =>
        {
            if (formOrigins.Length > 0)
            {
                policy.WithOrigins(formOrigins)
                    .WithMethods("POST")
                    .WithHeaders("Content-Type")
                    .SetPreflightMaxAge(TimeSpan.FromHours(1));
            }
        });

        // Read-only lookups for the same website: GET only, no credentials, no custom headers.
        options.AddPolicy(CorsPolicies.PublicReads, policy =>
        {
            if (formOrigins.Length > 0)
            {
                policy.WithOrigins(formOrigins).WithMethods("GET");
            }
        });
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
        // Development always migrates (local runs and the integration tests). A deployment
        // migrates only when Database:MigrateOnStartup is set: either the release pipeline
        // applies migrations before the app starts and leaves it off, or the database is on a
        // private network the pipeline cannot reach and the app does it itself. EF Core takes
        // a database lock while migrating, so several instances starting at once queue up
        // rather than racing.
        if (app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
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
    app.UseCors();
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

    /// <summary>Guards the public contact form.</summary>
    public const string PublicForm = "public-form";

    /// <summary>Guards the public postal-area lookups.</summary>
    public const string PublicLookup = "public-lookup";
}

public static class CorsPolicies
{
    /// <summary>The public website's contact form. See Cors:PublicFormOrigins.</summary>
    public const string PublicForms = "public-forms";

    /// <summary>The same website's read-only lookups. See Cors:PublicFormOrigins.</summary>
    public const string PublicReads = "public-reads";
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
