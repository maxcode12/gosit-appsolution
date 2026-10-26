using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Enquiries;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;
using static GoSit.IntegrationTests.Api.TrustAndSafetyTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>A host with a deliberately tiny contact-form limit.</summary>
public sealed class FormLimitedApiFactory : GoSitApiFactory
{
    public const int PermitLimit = 2;

    protected override IDictionary<string, string?> SettingOverrides =>
        new Dictionary<string, string?>
        {
            ["RateLimiting:FormPermitLimit"] = PermitLimit.ToString(),
        };
}

/// <summary>
/// The public contact form and the staff queue behind it.
///
/// The form is open to anyone on the internet and stores what strangers type, so most of
/// these tests are about what it refuses: no consent, scripts that fill the hidden field,
/// other websites, and floods.
/// </summary>
[Collection(nameof(ApiCollection))]
public class EnquiryTests(GoSitApiFactory factory)
{
    private const string AllowedOrigin = "https://www.gosits.test";

    private sealed record EnquiryBody(
        Guid EnquiryId, string Name, string Email, string Kind, string Message,
        string Source, string Status, DateTime CreatedAtUtc, DateTime? HandledAtUtc);

    private static object Valid(string email, string? website = null, bool consent = true) => new
    {
        name = "  Ada Lovelace ",
        email,
        kind = "Family",
        message = "I would like to know when you open in Mississauga.",
        consentGiven = consent,
        website,
    };

    private static string UniqueEmail() => $"visitor.{Guid.NewGuid():N}@example.com";

    private async Task<int> CountAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        return await context.Enquiries.CountAsync(e => e.Email == email);
    }

    // ---- the form itself ----

    [Fact]
    public async Task A_visitor_can_send_a_message_without_an_account()
    {
        var email = UniqueEmail();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/enquiries", Valid(email));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<GoSitDbContext>()
            .Enquiries.SingleAsync(e => e.Email == email);

        Assert.Equal("Ada Lovelace", saved.Name);
        Assert.Equal(EnquiryKind.Family, saved.Kind);
        Assert.Equal(EnquiryStatus.New, saved.Status);
        Assert.Equal("website", saved.Source);
    }

    [Fact]
    public async Task A_message_without_consent_is_refused_and_not_kept()
    {
        var email = UniqueEmail();

        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/enquiries", Valid(email, consent: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(email));
    }

    [Theory]
    [InlineData("not-an-email", "Family", "A perfectly fine message to read.")]
    [InlineData("someone@example.com", "Family", "short")]
    [InlineData("someone@example.com", "NotAKind", "A perfectly fine message to read.")]
    public async Task Malformed_messages_are_refused(string email, string kind, string message)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/enquiries", new
        {
            name = "Ada",
            email,
            kind,
            message,
            consentGiven = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_script_that_fills_the_hidden_field_is_told_it_worked_and_nothing_is_kept()
    {
        var email = UniqueEmail();

        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/enquiries", Valid(email, website: "http://spam.example"));

        // The same answer as a real submission, so the script learns nothing.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, await CountAsync(email));
    }

    [Fact]
    public async Task An_oversized_body_is_refused()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/enquiries", new
        {
            name = "Ada",
            email = UniqueEmail(),
            kind = "Family",
            message = new string('x', 40_000),
            consentGiven = true,
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest,
            $"expected the body to be refused, got {(int)response.StatusCode}");
    }

    // ---- which websites may call it ----

    [Fact]
    public async Task The_companys_own_website_is_allowed_to_call_the_form()
    {
        var response = await Preflight(AllowedOrigin);

        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Any_other_website_is_not()
    {
        var response = await Preflight("https://evil.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private Task<HttpResponseMessage> Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/enquiries");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        return factory.CreateClient().SendAsync(request);
    }

    // ---- the staff queue ----

    [Fact]
    public async Task Only_staff_can_read_the_queue()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/v1/admin/enquiries")).StatusCode);

        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await caregiver.GetAsync("/api/v1/admin/enquiries")).StatusCode);
    }

    [Fact]
    public async Task Staff_see_new_messages_and_can_close_them_once()
    {
        var email = UniqueEmail();
        await factory.CreateClient().PostAsJsonAsync("/api/v1/enquiries", Valid(email));

        var admin = await factory.RegisterAdminAsync();

        var queue = await admin.GetFromJsonAsync<List<EnquiryBody>>("/api/v1/admin/enquiries?take=200");
        var mine = Assert.Single(queue!, e => e.Email == email);
        Assert.Equal("New", mine.Status);
        Assert.Equal("Family", mine.Kind);

        var handled = await admin.PostAsync($"/api/v1/admin/enquiries/{mine.EnquiryId}/handled", null);
        Assert.Equal(HttpStatusCode.NoContent, handled.StatusCode);

        // Out of the default queue, but still on record.
        var open = await admin.GetFromJsonAsync<List<EnquiryBody>>("/api/v1/admin/enquiries?take=200");
        Assert.DoesNotContain(open!, e => e.EnquiryId == mine.EnquiryId);

        var all = await admin.GetFromJsonAsync<List<EnquiryBody>>("/api/v1/admin/enquiries?includeClosed=true&take=200");
        Assert.Equal("Handled", all!.Single(e => e.EnquiryId == mine.EnquiryId).Status);

        // And it cannot be closed twice.
        var again = await admin.PostAsync($"/api/v1/admin/enquiries/{mine.EnquiryId}/spam", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Closing_an_enquiry_that_does_not_exist_is_a_404()
    {
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsync($"/api/v1/admin/enquiries/{Guid.NewGuid()}/handled", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class EnquiryRateLimitTests : IClassFixture<FormLimitedApiFactory>
{
    private readonly FormLimitedApiFactory _factory;

    public EnquiryRateLimitTests(FormLimitedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Repeated_submissions_from_one_address_are_throttled()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < FormLimitedApiFactory.PermitLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/v1/enquiries", new
            {
                name = "Ada",
                email = $"flood.{i}.{Guid.NewGuid():N}@example.com",
                kind = "Other",
                message = "Just checking whether anyone is there.",
                consentGiven = true,
            });
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/api/v1/enquiries", new
        {
            name = "Ada",
            email = $"flood.over.{Guid.NewGuid():N}@example.com",
            kind = "Other",
            message = "One more than the limit allows.",
            consentGiven = true,
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
