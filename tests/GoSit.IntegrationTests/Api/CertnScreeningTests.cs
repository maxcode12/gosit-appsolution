using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GoSit.Infrastructure.Screening;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;
using static GoSit.IntegrationTests.Api.TrustAndSafetyTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Stands in for Certn's servers: records every request and answers the two it is asked.
///
/// Answers are written from Certn's published API reference, not from a real response, so
/// these tests prove GoSit does what that reference says. Whether Certn does is what the first
/// run against their sandbox settles.
/// </summary>
public sealed class FakeCertnHandler : HttpMessageHandler
{
    public sealed record Seen(HttpMethod Method, string Path, string? Authorization, string Body);

    private readonly object _gate = new();
    private readonly List<Seen> _seen = [];
    private readonly Dictionary<string, (string Status, string? Score)> _cases = [];

    public bool FailOrders { get; set; }

    public IReadOnlyList<Seen> Requests
    {
        get { lock (_gate) { return _seen.ToList(); } }
    }

    /// <summary>What Certn will say about a case, from now on.</summary>
    public void SetCase(string caseId, string status, string? score)
    {
        lock (_gate) { _cases[caseId] = (status, score); }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;

        lock (_gate)
        {
            _seen.Add(new Seen(request.Method, path, request.Headers.Authorization?.ToString(), body));
        }

        if (request.Method == HttpMethod.Post && path == "/api/public/cases/order/")
        {
            if (FailOrders)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            var caseId = Guid.NewGuid().ToString();
            SetCase(caseId, "APPLICANT_INVITED", null);

            return Json(HttpStatusCode.Created, new { id = caseId });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/public/cases/", StringComparison.Ordinal))
        {
            var caseId = path["/api/public/cases/".Length..].Trim('/');

            lock (_gate)
            {
                if (_cases.TryGetValue(caseId, out var known))
                {
                    return Json(HttpStatusCode.OK, new
                    {
                        id = caseId,
                        overall_status = known.Status,
                        overall_score = known.Score,
                    });
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    // One instance serves every client the factory builds, so disposing it must do nothing.
    protected override void Dispose(bool disposing)
    {
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };
}

/// <summary>A host with Certn configured and talking to the stand-in.</summary>
public sealed class CertnApiFactory : GoSitApiFactory
{
    public const string ApiKey = "test-api-key";
    public const string WebhookSecret = "certn-test-signing-secret";

    public FakeCertnHandler Certn { get; } = new();

    protected override IDictionary<string, string?> SettingOverrides =>
        new Dictionary<string, string?>
        {
            ["Certn:ApiKey"] = ApiKey,
            ["Certn:BaseUrl"] = "https://certn.test",
            ["Certn:WebhookSecret"] = WebhookSecret,
        };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
            services.AddHttpClient<CertnBackgroundCheckProvider>()
                .ConfigurePrimaryHttpMessageHandler(() => Certn));
    }
}

/// <summary>
/// The Certn connection, end to end: a caregiver starts a check, Certn is asked to run it, a
/// signed message says it has moved, and staff see what Certn reported.
///
/// What is held constant throughout is that Certn never decides. A clear result is evidence on
/// the case; the case stays in review until a staff member approves it.
/// </summary>
public class CertnScreeningTests : IClassFixture<CertnApiFactory>
{
    private readonly CertnApiFactory _factory;

    public CertnScreeningTests(CertnApiFactory factory) => _factory = factory;

    private sealed record CaseBody(Guid Id, string CheckType, string Status);

    private sealed record SummaryBody(string VerificationStatus, IReadOnlyList<CaseBody> Cases);

    private sealed record QueueBody(
        Guid CaseId, string CheckType, string Status, string? ProviderStatus, string? ProviderScore);

    private sealed record MeBody(string Email);

    private async Task<(HttpClient Caregiver, string Email)> NewCaregiverAsync()
    {
        var id = await _factory.CreateBookableCaregiverAsync();
        var client = await _factory.CaregiverClientForAsync(id);
        var me = await client.GetFromJsonAsync<MeBody>("/api/v1/me");

        return (client, me!.Email);
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient caregiver, string checkType) =>
        caregiver.PostAsJsonAsync("/api/v1/caregivers/me/verifications", new { checkType, documentReference = (string?)null });

    private static async Task<CaseBody> OnlyCaseAsync(HttpClient caregiver, string checkType)
    {
        var summary = await caregiver.GetFromJsonAsync<SummaryBody>("/api/v1/caregivers/me/verifications");
        return Assert.Single(summary!.Cases, c => c.CheckType == checkType);
    }

    private static string Signed(string json) =>
        CertnWebhookSignature.Sign(Encoding.UTF8.GetBytes(json), CertnApiFactory.WebhookSecret);

    private static HttpRequestMessage Message(string json, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/certn")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        if (signature is not null)
        {
            request.Headers.Add("X-Signature", signature);
        }

        return request;
    }

    private static string CaseEvent(string caseId, string eventType = "CASE_STATUS_CHANGED") =>
        JsonSerializer.Serialize(new
        {
            created = DateTime.UtcNow,
            event_id = Guid.NewGuid().ToString(),
            event_type = eventType,
            object_id = caseId,
            object_type = "CASE",
            case_status = "COMPLETE",
        });

    // ---- starting a check ----

    [Fact]
    public async Task Starting_an_identity_check_orders_it_from_certn_with_only_the_email_address()
    {
        var (caregiver, email) = await NewCaregiverAsync();
        var before = _factory.Certn.Requests.Count;

        var response = await StartAsync(caregiver, "Identity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = Assert.Single(_factory.Certn.Requests.Skip(before));
        Assert.Equal(HttpMethod.Post, order.Method);
        Assert.Equal("/api/public/cases/order/", order.Path);
        Assert.Equal($"Api-Key {CertnApiFactory.ApiKey}", order.Authorization);

        using var body = JsonDocument.Parse(order.Body);
        Assert.Equal(email, body.RootElement.GetProperty("email_address").GetString());
        Assert.True(body.RootElement.GetProperty("send_invite_email").GetBoolean());
        Assert.True(body.RootElement.GetProperty("check_types_with_arguments").TryGetProperty("IDENTITY_VERIFICATION_1", out _));

        // The caregiver's name and anything else about them is Certn's to collect, not ours to send.
        Assert.Equal(3, body.RootElement.EnumerateObject().Count());

        Assert.Equal("InReview", (await OnlyCaseAsync(caregiver, "Identity")).Status);
    }

    [Fact]
    public async Task A_criminal_record_check_asks_for_the_canadian_report()
    {
        var (caregiver, _) = await NewCaregiverAsync();
        var before = _factory.Certn.Requests.Count;

        await StartAsync(caregiver, "BackgroundCheck");

        var order = Assert.Single(_factory.Certn.Requests.Skip(before));
        Assert.Contains("CRIMINAL_RECORD_REPORT_1", order.Body);
        Assert.Contains("BASIC_CANADIAN_CRIMINAL_RECORD_REPORT_1", order.Body);
    }

    /// <summary>
    /// Certn's older API lists a vulnerable sector check and the current one has not been
    /// confirmed to. Ordering a guessed check for someone who will care for children would be
    /// worse than leaving it with staff, so nothing is sent and the case waits for review.
    /// </summary>
    [Fact]
    public async Task The_vulnerable_sector_check_is_left_to_staff_and_never_sent_to_certn()
    {
        var (caregiver, _) = await NewCaregiverAsync();
        var before = _factory.Certn.Requests.Count;

        var response = await StartAsync(caregiver, "VulnerableSector");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Certn.Requests.Skip(before));
        Assert.Equal("Submitted", (await OnlyCaseAsync(caregiver, "VulnerableSector")).Status);
    }

    [Fact]
    public async Task When_certn_is_down_the_caregiver_is_told_to_try_again_and_no_check_is_recorded()
    {
        var (caregiver, _) = await NewCaregiverAsync();
        _factory.Certn.FailOrders = true;

        try
        {
            var response = await StartAsync(caregiver, "Identity");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var summary = await caregiver.GetFromJsonAsync<SummaryBody>("/api/v1/caregivers/me/verifications");
            Assert.DoesNotContain(summary!.Cases, c => c.CheckType == "Identity");
        }
        finally
        {
            _factory.Certn.FailOrders = false;
        }
    }

    // ---- Certn's notifications ----

    [Fact]
    public async Task Certn_can_prove_it_owns_the_address_by_having_a_token_echoed_back()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/webhooks/certn?challenge=abc123-XYZ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("abc123-XYZ", await response.Content.ReadAsStringAsync());
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType!.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("a b")]
    public async Task Nothing_that_could_be_taken_for_a_page_is_ever_echoed(string challenge)
    {
        var response = await _factory.CreateClient()
            .GetAsync($"/api/v1/webhooks/certn?challenge={Uri.EscapeDataString(challenge)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-hex")]
    [InlineData("deadbeef")]
    public async Task A_message_with_a_missing_or_wrong_signature_is_refused_and_changes_nothing(string? signature)
    {
        var (caregiver, _) = await NewCaregiverAsync();
        await StartAsync(caregiver, "Identity");
        var theCase = await OnlyCaseAsync(caregiver, "Identity");
        var reference = ReferenceFor(theCase.Id);

        // Certn would say this case is clear; a forger might say the same. Only a signed message counts.
        _factory.Certn.SetCase(reference, "COMPLETE", "CLEAR");
        var forged = CaseEvent(reference);

        var response = await _factory.CreateClient().SendAsync(Message(forged, signature));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var admin = await _factory.RegisterAdminAsync();
        var queued = (await admin.GetFromJsonAsync<List<QueueBody>>("/api/v1/admin/verifications"))!
            .Single(c => c.CaseId == theCase.Id);
        Assert.Null(queued.ProviderScore);
    }

    [Fact]
    public async Task A_signed_message_records_what_certn_reported_and_staff_still_decide()
    {
        var (caregiver, _) = await NewCaregiverAsync();
        await StartAsync(caregiver, "Identity");
        var theCase = await OnlyCaseAsync(caregiver, "Identity");

        var admin = await _factory.RegisterAdminAsync();
        var reference = ReferenceFor(theCase.Id);

        _factory.Certn.SetCase(reference, "COMPLETE", "CLEAR");

        var json = CaseEvent(reference);
        var response = await _factory.CreateClient().SendAsync(Message(json, Signed(json)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Staff see Certn's word beside the case...
        var queued = (await admin.GetFromJsonAsync<List<QueueBody>>("/api/v1/admin/verifications"))!
            .Single(c => c.CaseId == theCase.Id);
        Assert.Equal("COMPLETE", queued.ProviderStatus);
        Assert.Equal("CLEAR", queued.ProviderScore);

        // ...but a clear result approves nobody. The case is still waiting for a person.
        Assert.Equal("InReview", queued.Status);
        Assert.Equal("InReview", (await OnlyCaseAsync(caregiver, "Identity")).Status);
    }

    [Fact]
    public async Task The_same_message_delivered_twice_does_no_harm()
    {
        var (caregiver, _) = await NewCaregiverAsync();
        await StartAsync(caregiver, "BackgroundCheck");
        var theCase = await OnlyCaseAsync(caregiver, "BackgroundCheck");

        var admin = await _factory.RegisterAdminAsync();
        var reference = ReferenceFor(theCase.Id);
        _factory.Certn.SetCase(reference, "COMPLETE", "REVIEW");

        var json = CaseEvent(reference);

        for (var i = 0; i < 2; i++)
        {
            var response = await _factory.CreateClient().SendAsync(Message(json, Signed(json)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var queued = (await admin.GetFromJsonAsync<List<QueueBody>>("/api/v1/admin/verifications"))!
            .Single(c => c.CaseId == theCase.Id);
        Assert.Equal("REVIEW", queued.ProviderScore);
    }

    [Fact]
    public async Task A_message_about_a_case_we_do_not_hold_is_acknowledged_and_ignored()
    {
        var json = CaseEvent(Guid.NewGuid().ToString());

        var response = await _factory.CreateClient().SendAsync(Message(json, Signed(json)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_message_that_is_not_json_is_acknowledged_so_it_is_not_retried_for_hours()
    {
        const string garbage = "this is not json";

        var response = await _factory.CreateClient().SendAsync(Message(garbage, Signed(garbage)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Events_that_are_not_about_a_case_are_ignored()
    {
        var json = JsonSerializer.Serialize(new
        {
            event_id = Guid.NewGuid().ToString(),
            event_type = "SOMETHING_NEW",
            object_id = Guid.NewGuid().ToString(),
            object_type = "CASE",
        });
        var before = _factory.Certn.Requests.Count;

        var response = await _factory.CreateClient().SendAsync(Message(json, Signed(json)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, _factory.Certn.Requests.Count);
    }

    [Fact]
    public async Task A_late_message_cannot_rewrite_the_evidence_after_staff_have_decided()
    {
        var (caregiver, _) = await NewCaregiverAsync();
        await StartAsync(caregiver, "Identity");
        var theCase = await OnlyCaseAsync(caregiver, "Identity");

        var admin = await _factory.RegisterAdminAsync();
        var reference = ReferenceFor(theCase.Id);

        _factory.Certn.SetCase(reference, "COMPLETE", "CLEAR");
        var first = CaseEvent(reference);
        await _factory.CreateClient().SendAsync(Message(first, Signed(first)));

        var approve = await admin.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{theCase.Id}/approve", new { validUntil = (string?)null, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // Certn later says something different. The decision was made on the evidence above.
        _factory.Certn.SetCase(reference, "COMPLETE", "REJECT");
        var late = CaseEvent(reference);
        var response = await _factory.CreateClient().SendAsync(Message(late, Signed(late)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Approved", (await OnlyCaseAsync(caregiver, "Identity")).Status);
    }

    /// <summary>
    /// The staff queue does not expose Certn's case id, so the test reads it where the app
    /// keeps it. It is the one place a test needs to look inside.
    /// </summary>
    private string ReferenceFor(Guid caseId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSit.Infrastructure.Persistence.GoSitDbContext>();

        var reference = context.VerificationCases
            .Where(c => c.Id == caseId)
            .Select(c => c.ProviderReference)
            .Single();

        Assert.False(string.IsNullOrWhiteSpace(reference));
        return reference!;
    }
}

/// <summary>With no signing secret configured, notifications cannot be trusted and are all refused.</summary>
[Collection(nameof(ApiCollection))]
public class CertnWithoutSecretTests(GoSitApiFactory factory)
{
    [Fact]
    public async Task Messages_are_refused_when_there_is_no_signing_secret_to_check_them_against()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/certn")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Signature", "00");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_certn_key_every_check_waits_for_staff_as_before()
    {
        var id = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(id);

        var response = await caregiver.PostAsJsonAsync(
            "/api/v1/caregivers/me/verifications",
            new { checkType = "BackgroundCheck", documentReference = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await caregiver.GetStringAsync("/api/v1/caregivers/me/verifications");
        Assert.Contains("\"Submitted\"", summary);
    }
}
