using System.Net;
using System.Net.Http.Json;
using GoSit.Domain.Caregivers;
using GoSit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;
using static GoSit.IntegrationTests.Api.TrustAndSafetyTestFixtures;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class AdministrationTests(GoSitApiFactory factory)
{
    private sealed record VerificationCaseBody(
        Guid Id, string CheckType, string Status, DateOnly? ValidUntil,
        string? DecisionNotes, DateTime SubmittedAtUtc, DateTime? ReviewedAtUtc);

    private sealed record VerificationSummaryBody(
        string VerificationStatus, bool MeetsListingRequirements,
        IReadOnlyList<VerificationCaseBody> Cases);

    private sealed record IncidentBody(
        Guid Id, string Category, string Severity, string Status, DateTime CreatedAtUtc);

    private sealed record AdminIncidentBody(
        Guid Id, Guid ReportedByUserId, Guid SubjectUserId, Guid? CareBookingId,
        string Category, string Severity, string Status, bool RequiresUrgentReview,
        string Description, Guid? AssignedToUserId, DateTime CreatedAtUtc);

    private static Task<HttpResponseMessage> SubmitVerificationAsync(
        HttpClient caregiver, string checkType) =>
        caregiver.PostAsJsonAsync("/api/v1/caregivers/me/verifications", new
        {
            checkType,
            documentReference = "blob/private-evidence-key",
        });

    // ---- authorization: the gate on everything staff can do ----

    /// <summary>
    /// A caregiver approving their own verification would defeat the entire point of
    /// the marketplace.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_reach_any_admin_route()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        foreach (var route in new[] { "/api/v1/admin/verifications", "/api/v1/admin/incidents" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await caregiver.GetAsync(route)).StatusCode);
        }

        var approve = await caregiver.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{Guid.NewGuid()}/approve", new { });
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    [Fact]
    public async Task A_care_requester_cannot_reach_any_admin_route()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await requester.GetAsync("/api/v1/admin/incidents")).StatusCode);

        var suspend = await requester.PostAsJsonAsync(
            $"/api/v1/admin/users/{Guid.NewGuid()}/suspend", new { reason = "I do not like them." });
        Assert.Equal(HttpStatusCode.Forbidden, suspend.StatusCode);
    }

    [Fact]
    public async Task Admin_routes_require_authentication()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/admin/incidents")).StatusCode);
    }

    // ---- verification workflow ----

    [Fact]
    public async Task A_submitted_check_starts_pending_and_confers_nothing()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"],
            availability: [(DayOfWeek.Monday, "09:00:00", "17:00:00")]);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);

        var response = await SubmitVerificationAsync(caregiver, "Identity");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await caregiver.GetFromJsonAsync<VerificationSummaryBody>(
            "/api/v1/caregivers/me/verifications");

        Assert.Equal("Submitted", summary!.Cases.Single().Status);
        Assert.False(summary.MeetsListingRequirements);
    }

    /// <summary>
    /// The uploaded evidence is a private Blob Storage key and must never come back out.
    /// </summary>
    [Fact]
    public async Task The_verification_document_reference_is_never_returned()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await SubmitVerificationAsync(caregiver, "Identity");

        var body = await caregiver.GetStringAsync("/api/v1/caregivers/me/verifications");

        Assert.DoesNotContain("private-evidence-key", body);
        Assert.DoesNotContain("documentReference", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_same_check_cannot_be_submitted_twice_while_it_is_live()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        await SubmitVerificationAsync(caregiver, "Identity");

        var second = await SubmitVerificationAsync(caregiver, "Identity");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Approving one check is not clearance to work — a caregiver becomes verified only
    /// once every required check passes.
    /// </summary>
    [Fact]
    public async Task One_approved_check_does_not_make_a_caregiver_verified()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"],
            availability: [(DayOfWeek.Monday, "09:00:00", "17:00:00")]);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var admin = await factory.RegisterAdminAsync();

        await SubmitVerificationAsync(caregiver, "Identity");
        var pending = await admin.GetFromJsonAsync<List<PendingVerificationBody>>(
            "/api/v1/admin/verifications");
        var identityCase = pending!.Single(c => c.CaregiverProfileId == caregiverId);

        var approve = await admin.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{identityCase.CaseId}/approve",
            new { validUntil = (DateOnly?)null, notes = "ID matched." });

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var summary = await caregiver.GetFromJsonAsync<VerificationSummaryBody>(
            "/api/v1/caregivers/me/verifications");

        // Identity alone is not enough to work with children.
        Assert.False(summary!.MeetsListingRequirements);
        Assert.NotEqual("Verified", summary.VerificationStatus);
    }

    [Fact]
    public async Task Approving_every_required_check_verifies_the_caregiver()
    {
        var caregiverId = await factory.CreateListableCaregiverAsync(
            serviceCodes: ["BABYSITTING"], certificationCodes: ["VULNERABLE_SECTOR"],
            availability: [(DayOfWeek.Monday, "09:00:00", "17:00:00")]);
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var admin = await factory.RegisterAdminAsync();

        foreach (var checkType in new[] { "Identity", "VulnerableSector" })
        {
            await SubmitVerificationAsync(caregiver, checkType);
        }

        var pending = await admin.GetFromJsonAsync<List<PendingVerificationBody>>(
            "/api/v1/admin/verifications?take=200");

        foreach (var c in pending!.Where(c => c.CaregiverProfileId == caregiverId))
        {
            await admin.PostAsJsonAsync(
                $"/api/v1/admin/verifications/{c.CaseId}/approve",
                new { validUntil = (DateOnly?)null, notes = "Checked." });
        }

        var summary = await caregiver.GetFromJsonAsync<VerificationSummaryBody>(
            "/api/v1/caregivers/me/verifications");

        Assert.True(summary!.MeetsListingRequirements);
        Assert.Equal("Verified", summary.VerificationStatus);
    }

    [Fact]
    public async Task Rejecting_a_required_check_delists_the_caregiver()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var admin = await factory.RegisterAdminAsync();

        await SubmitVerificationAsync(caregiver, "VulnerableSector");
        var pending = await admin.GetFromJsonAsync<List<PendingVerificationBody>>(
            "/api/v1/admin/verifications?take=200");
        var target = pending!.Single(c => c.CaregiverProfileId == caregiverId);

        var reject = await admin.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{target.CaseId}/reject",
            new { reason = "The document was unreadable." });

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        var status = await context.CaregiverProfiles
            .Where(p => p.Id == caregiverId)
            .Select(p => p.VerificationStatus)
            .SingleAsync();

        Assert.Equal(VerificationStatus.Rejected, status);
    }

    [Fact]
    public async Task A_rejection_reason_is_required()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var admin = await factory.RegisterAdminAsync();

        await SubmitVerificationAsync(caregiver, "Identity");
        var pending = await admin.GetFromJsonAsync<List<PendingVerificationBody>>(
            "/api/v1/admin/verifications?take=200");
        var target = pending!.Single(c => c.CaregiverProfileId == caregiverId);

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{target.CaseId}/reject", new { reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- incident reporting ----

    [Fact]
    public async Task Any_signed_in_user_can_report_another()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var subjectUserId = await factory.UserIdForCaregiverAsync(caregiverId);
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/reports", new
        {
            subjectUserId,
            careBookingId = (Guid?)null,
            category = "SafetyConcern",
            severity = "High",
            description = "Sam was left unattended.",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<IncidentBody>())!;
        Assert.Equal("Open", report.Status);
    }

    /// <summary>
    /// The report describes a real person and may name a child. The reporter's own
    /// response must not echo internal triage state back.
    /// </summary>
    [Fact]
    public async Task A_reporter_does_not_see_internal_triage_fields()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var subjectUserId = await factory.UserIdForCaregiverAsync(caregiverId);
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.PostAsJsonAsync("/api/v1/reports", new
        {
            subjectUserId,
            careBookingId = (Guid?)null,
            category = "SafetyConcern",
            severity = "High",
            description = "Sam was left unattended.",
        });

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("assignedTo", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resolutionNotes", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_report_cannot_be_raised_about_yourself()
    {
        var requester = await factory.RegisterCareRequesterAsync();
        var me = await requester.GetFromJsonAsync<MeBody>("/api/v1/me");

        var response = await requester.PostAsJsonAsync("/api/v1/reports", new
        {
            subjectUserId = me!.Id,
            careBookingId = (Guid?)null,
            category = "Other",
            severity = "Low",
            description = "Testing.",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Safety concerns jump the queue whatever severity the reporter chose.</summary>
    [Fact]
    public async Task A_safety_concern_is_flagged_urgent_in_the_triage_queue()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var subjectUserId = await factory.UserIdForCaregiverAsync(caregiverId);
        var requester = await factory.RegisterCareRequesterAsync();

        var raised = await requester.PostAsJsonAsync("/api/v1/reports", new
        {
            subjectUserId,
            careBookingId = (Guid?)null,
            category = "SafetyConcern",
            severity = "Low",
            description = "Concerned about supervision.",
        });

        var reportId = (await raised.Content.ReadFromJsonAsync<IncidentBody>())!.Id;

        var admin = await factory.RegisterAdminAsync();
        var queue = await admin.GetFromJsonAsync<List<AdminIncidentBody>>(
            "/api/v1/admin/incidents?take=200");

        var report = queue!.Single(r => r.Id == reportId);
        Assert.True(report.RequiresUrgentReview);
    }

    [Fact]
    public async Task Staff_can_close_a_report_with_an_explanation()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var subjectUserId = await factory.UserIdForCaregiverAsync(caregiverId);
        var requester = await factory.RegisterCareRequesterAsync();

        var raised = await requester.PostAsJsonAsync("/api/v1/reports", new
        {
            subjectUserId,
            careBookingId = (Guid?)null,
            category = "NoShow",
            severity = "Low",
            description = "Did not arrive.",
        });

        var reportId = (await raised.Content.ReadFromJsonAsync<IncidentBody>())!.Id;
        var admin = await factory.RegisterAdminAsync();

        var closed = await admin.PostAsJsonAsync(
            $"/api/v1/admin/incidents/{reportId}/close",
            new { notes = "Contacted both parties; caregiver was unwell.", dismiss = false });

        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);
    }

    /// <summary>An outcome with no reasoning is not an audit trail.</summary>
    [Fact]
    public async Task Closing_a_report_without_an_explanation_is_rejected()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var subjectUserId = await factory.UserIdForCaregiverAsync(caregiverId);
        var requester = await factory.RegisterCareRequesterAsync();

        var raised = await requester.PostAsJsonAsync("/api/v1/reports", new
        {
            subjectUserId,
            careBookingId = (Guid?)null,
            category = "NoShow",
            severity = "Low",
            description = "Did not arrive.",
        });

        var reportId = (await raised.Content.ReadFromJsonAsync<IncidentBody>())!.Id;
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/incidents/{reportId}/close", new { notes = "", dismiss = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- suspension ----

    /// <summary>
    /// Suspending revokes every refresh token, so the account is signed out everywhere
    /// rather than staying live until its tokens expire.
    /// </summary>
    [Fact]
    public async Task Suspending_an_account_stops_it_authenticating()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var caregiver = await factory.CaregiverClientForAsync(caregiverId);
        var subjectUserId = await factory.UserIdForCaregiverAsync(caregiverId);
        var admin = await factory.RegisterAdminAsync();

        // Works before suspension.
        Assert.Equal(HttpStatusCode.OK, (await caregiver.GetAsync("/api/v1/me")).StatusCode);

        var suspended = await admin.PostAsJsonAsync(
            $"/api/v1/admin/users/{subjectUserId}/suspend",
            new { reason = "Under investigation after a safety report." });

        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        var email = await context.Users.Where(u => u.Id == subjectUserId)
            .Select(u => u.Email).SingleAsync();

        // A suspended account cannot get new tokens even with correct credentials.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "correct-horse-battery-staple",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_suspend_themselves()
    {
        var admin = await factory.RegisterAdminAsync();
        var me = await admin.GetFromJsonAsync<MeBody>("/api/v1/me");

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/users/{me!.Id}/suspend", new { reason = "Oops." });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_reinstated_account_can_authenticate_again()
    {
        var caregiverId = await factory.CreateBookableCaregiverAsync();
        var subjectUserId = await factory.UserIdForCaregiverAsync(caregiverId);
        var admin = await factory.RegisterAdminAsync();

        await admin.PostAsJsonAsync(
            $"/api/v1/admin/users/{subjectUserId}/suspend", new { reason = "Investigating." });
        await admin.PostAsync($"/api/v1/admin/users/{subjectUserId}/reinstate", null);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GoSitDbContext>();
        var email = await context.Users.Where(u => u.Id == subjectUserId)
            .Select(u => u.Email).SingleAsync();

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "correct-horse-battery-staple",
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private sealed record PendingVerificationBody(
        Guid CaseId, Guid CaregiverProfileId, string CheckType,
        string Status, DateTime SubmittedAtUtc);

    private sealed record MeBody(
        Guid Id, string Email, string FirstName, string LastName, string Role, bool EmailVerified);
}
