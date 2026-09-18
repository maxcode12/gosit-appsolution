using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GoSit.Domain.Caregivers;
using GoSit.Infrastructure.Persistence;
using GoSit.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Caregiver insurance: GoSit verifies a caregiver's own coverage, and never issues or
/// holds it — see docs/BUSINESS_MODEL.md and docs/ADR-004-marketplace-provider-model.md.
/// This mirrors CertificationReviewTests and CertificationDocumentTests closely, since
/// the review workflow is the same shape by design.
/// </summary>
[Collection(nameof(ApiCollection))]
public class CaregiverInsuranceTests(GoSitApiFactory factory)
{
    private sealed record PolicyBody(
        Guid Id, string CoverageType, string Insurer, string PolicyReference,
        decimal LiabilityLimit, DateOnly EffectiveOn, DateOnly ExpiresOn, string Status, bool IsValid);

    private sealed record ProfileBody(IReadOnlyList<PolicyBody> InsurancePolicies);

    private sealed record PendingInsuranceBody(
        Guid CaregiverInsurancePolicyId, Guid CaregiverProfileId, Guid CaregiverUserId,
        string CaregiverName, string CaregiverEmail, string CoverageType, string Insurer,
        string PolicyReference, decimal LiabilityLimit, DateOnly EffectiveOn, DateOnly ExpiresOn,
        bool IsExpired, bool HasDocument, DateTime SubmittedAtUtc);

    private static object Policy(string coverageType = "GeneralLiability") => new
    {
        coverageType,
        insurer = "Aviva Canada",
        policyReference = "POL-12345",
        liabilityLimit = 2_000_000m,
        effectiveOn = "2026-01-01",
        expiresOn = "2030-01-01",
    };

    /// <summary>A caregiver whose profile exists — insurance submission requires one.</summary>
    private async Task<HttpClient> CaregiverAsync()
    {
        var client = await factory.RegisterCaregiverAsync();

        (await client.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ten years with children.",
            yearsOfExperience = 10,
            hourlyRate = 28m,
            serviceRadiusKm = 15,
        })).EnsureSuccessStatusCode();

        return client;
    }

    private async Task<(HttpClient Caregiver, Guid PolicyId)> SubmittedAsync(
        string coverageType = "GeneralLiability")
    {
        var caregiver = await CaregiverAsync();

        var submitted = await caregiver.PostAsJsonAsync("/api/v1/caregivers/me/insurance", Policy(coverageType));
        submitted.EnsureSuccessStatusCode();

        var body = await submitted.Content.ReadFromJsonAsync<PolicyBody>();
        return (caregiver, body!.Id);
    }

    private static MultipartFormDataContent FileContent(string body = "policy declaration page") =>
        new()
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(body))
                { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } },
              "file", "policy.pdf" },
        };

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid policyId, MultipartFormDataContent content) =>
        client.PostAsync($"/api/v1/caregivers/me/insurance/{policyId}/document", content);

    // ---- submission ----

    [Fact]
    public async Task A_submitted_policy_confers_nothing_until_it_is_reviewed()
    {
        var (caregiver, _) = await SubmittedAsync();

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        var policy = Assert.Single(profile!.InsurancePolicies);

        Assert.Equal("Pending", policy.Status);
        Assert.False(policy.IsValid);
    }

    [Fact]
    public async Task An_already_expired_policy_is_refused()
    {
        var caregiver = await CaregiverAsync();

        var response = await caregiver.PostAsJsonAsync("/api/v1/caregivers/me/insurance", new
        {
            coverageType = "GeneralLiability",
            insurer = "Aviva Canada",
            policyReference = "POL-1",
            liabilityLimit = 2_000_000m,
            effectiveOn = "2020-01-01",
            expiresOn = "2021-01-01",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_zero_liability_limit_is_refused()
    {
        var caregiver = await CaregiverAsync();

        var response = await caregiver.PostAsJsonAsync("/api/v1/caregivers/me/insurance", new
        {
            coverageType = "GeneralLiability",
            insurer = "Aviva Canada",
            policyReference = "POL-1",
            liabilityLimit = 0m,
            effectiveOn = "2026-01-01",
            expiresOn = "2030-01-01",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- the document gate, mirroring certifications ----

    [Fact]
    public async Task Staff_cannot_approve_a_policy_with_no_document()
    {
        var (_, policyId) = await SubmittedAsync();
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsync($"/api/v1/admin/insurance/{policyId}/approve", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Staff_can_approve_once_a_document_is_uploaded()
    {
        var (caregiver, policyId) = await SubmittedAsync();
        (await UploadAsync(caregiver, policyId, FileContent())).EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();
        var response = await admin.PostAsync($"/api/v1/admin/insurance/{policyId}/approve", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        var policy = Assert.Single(profile!.InsurancePolicies);

        Assert.Equal("Verified", policy.Status);
        Assert.True(policy.IsValid);
    }

    [Fact]
    public async Task Rejecting_records_the_reason()
    {
        var (caregiver, policyId) = await SubmittedAsync();
        var admin = await factory.RegisterAdminAsync();

        var reject = await admin.PostAsJsonAsync(
            $"/api/v1/admin/insurance/{policyId}/reject", new { reason = "The declaration page is not legible." });

        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        Assert.Equal("Rejected", Assert.Single(profile!.InsurancePolicies).Status);
    }

    [Fact]
    public async Task A_rejection_must_carry_a_reason()
    {
        var (_, policyId) = await SubmittedAsync();
        var admin = await factory.RegisterAdminAsync();

        var reject = await admin.PostAsJsonAsync($"/api/v1/admin/insurance/{policyId}/reject", new { reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, reject.StatusCode);
    }

    [Fact]
    public async Task Replacing_the_document_sends_the_policy_back_for_review()
    {
        var (caregiver, policyId) = await SubmittedAsync();
        (await UploadAsync(caregiver, policyId, FileContent("first"))).EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();
        (await admin.PostAsync($"/api/v1/admin/insurance/{policyId}/approve", null)).EnsureSuccessStatusCode();

        (await UploadAsync(caregiver, policyId, FileContent("second"))).EnsureSuccessStatusCode();

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        Assert.Equal("Pending", profile!.InsurancePolicies.Single(p => p.Id == policyId).Status);

        var document = await admin.GetAsync($"/api/v1/admin/insurance/{policyId}/document");
        Assert.Equal("second", await document.Content.ReadAsStringAsync());
    }

    // ---- authorization ----

    [Fact]
    public async Task A_caregiver_cannot_approve_their_own_policy()
    {
        var (caregiver, policyId) = await SubmittedAsync();

        var response = await caregiver.PostAsync($"/api/v1/admin/insurance/{policyId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_care_requester_cannot_read_the_insurance_queue()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var queue = await requester.GetAsync("/api/v1/admin/insurance");

        Assert.Equal(HttpStatusCode.Forbidden, queue.StatusCode);
    }

    [Fact]
    public async Task Uploading_to_another_caregivers_policy_is_not_found()
    {
        var (_, policyId) = await SubmittedAsync();
        var stranger = await CaregiverAsync();

        var response = await UploadAsync(stranger, policyId, FileContent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_policy_is_not_found()
    {
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsync($"/api/v1/admin/insurance/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>The queue names the caregiver — nobody can decide from a bare profile id.</summary>
    [Fact]
    public async Task The_queue_names_the_caregiver()
    {
        var (_, policyId) = await SubmittedAsync("Automobile");
        var admin = await factory.RegisterAdminAsync();

        var queue = await admin.GetFromJsonAsync<List<PendingInsuranceBody>>("/api/v1/admin/insurance");

        var row = Assert.Single(queue!, p => p.CaregiverInsurancePolicyId == policyId);

        Assert.False(string.IsNullOrWhiteSpace(row.CaregiverName));
        Assert.Equal("Automobile", row.CoverageType);
        Assert.False(row.IsExpired);
    }

    // ---- expiry ----

    private Task<InsuranceExpirySweepResult> SweepAsync() =>
        factory.Services.GetRequiredService<InsuranceExpirySweeper>().SweepAsync(CancellationToken.None);

    private async Task ExpireOnAsync(Guid caregiverProfileId, DateOnly expiresOn)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();

        await context.Set<CaregiverInsurancePolicy>()
            .Where(p => p.CaregiverProfileId == caregiverProfileId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiresOn, expiresOn), CancellationToken.None);
    }

    private sealed record NotificationBody(string Type, string Title, string? Body, string? RelatedEntityType);

    private sealed record NotificationListBody(IReadOnlyList<NotificationBody> Notifications, int UnreadCount);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task A_caregiver_is_warned_before_a_verified_policy_lapses()
    {
        var (caregiver, policyId) = await SubmittedAsync();
        (await UploadAsync(caregiver, policyId, FileContent())).EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();
        (await admin.PostAsync($"/api/v1/admin/insurance/{policyId}/approve", null)).EnsureSuccessStatusCode();

        var profileId = (await caregiver.GetFromJsonAsync<ProfileIdBody>("/api/v1/caregivers/me"))!.Id;
        await ExpireOnAsync(profileId, Today.AddDays(10));

        await SweepAsync();

        var notifications = await caregiver.GetFromJsonAsync<NotificationListBody>("/api/v1/notifications");

        var warning = Assert.Single(
            notifications!.Notifications, n => n.Type == "InsuranceExpiring");

        Assert.Equal("CaregiverInsurancePolicy", warning.RelatedEntityType);
        Assert.Contains("expires soon", warning.Title);
    }

    private sealed record ProfileIdBody(Guid Id);
}
