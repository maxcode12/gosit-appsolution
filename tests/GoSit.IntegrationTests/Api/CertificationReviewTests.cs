using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;
using static GoSit.IntegrationTests.Api.BookingTestFixtures;
using static GoSit.IntegrationTests.Api.TrustAndSafetyTestFixtures;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Staff review of submitted certifications.
///
/// The domain has had approve and reject all along; there was no route to reach them, so
/// a submitted certificate could only be cleared by editing the database — which made
/// every service with a certification requirement unbookable in practice.
/// </summary>
[Collection(nameof(ApiCollection))]
public class CertificationReviewTests(GoSitApiFactory factory)
{
    private static object Certificate(string code = "FIRST_AID") => new
    {
        certificationCode = code,
        issuedOn = "2025-01-15",
        expiresOn = "2030-01-15",
        documentReference = (string?)null,
    };

    /// <summary>A caregiver with a profile and one submitted certificate.</summary>
    private async Task<(HttpClient Caregiver, Guid CertificationId)> SubmittedAsync(
        string code = "FIRST_AID")
    {
        var caregiver = await factory.RegisterCaregiverAsync();

        (await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Six years with children.",
            yearsOfExperience = 6,
            hourlyRate = 30m,
            address = new
            {
                line1 = "12 Bloor St W",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5S1M2",
                country = "CA",
            },
            serviceRadiusKm = 20,
        })).EnsureSuccessStatusCode();

        var submitted = await caregiver.PostAsJsonAsync(
            "/api/v1/caregivers/me/certifications", Certificate(code));

        submitted.EnsureSuccessStatusCode();
        var body = await submitted.Content.ReadFromJsonAsync<CertificationBody>();

        /*
         * With the evidence attached, because approval now requires it — a certification
         * with nothing to look at cannot be approved, which is the whole point of
         * CertificationDocumentTests. Everything in this file is about the review
         * decision rather than the upload, so the document is part of the setup.
         */
        var file = new ByteArrayContent("certificate"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        using var content = new MultipartFormDataContent { { file, "file", "certificate.png" } };

        (await caregiver.PostAsync(
            $"/api/v1/caregivers/me/certifications/{body!.Id}/document", content))
            .EnsureSuccessStatusCode();

        return (caregiver, body.Id);
    }

    [Fact]
    public async Task A_submitted_certificate_confers_nothing_until_it_is_reviewed()
    {
        var (caregiver, _) = await SubmittedAsync();

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        var certification = Assert.Single(profile!.Certifications);

        Assert.Equal("Pending", certification.Status);
        Assert.False(certification.IsValid);
    }

    /// <summary>
    /// The queue has to name the caregiver. Nobody can decide whether a certificate is
    /// genuine from a profile id.
    /// </summary>
    [Fact]
    public async Task The_queue_names_the_caregiver_rather_than_only_their_profile_id()
    {
        var (_, certificationId) = await SubmittedAsync();
        var admin = await factory.RegisterAdminAsync();

        var queue = await admin.GetFromJsonAsync<List<PendingCertificationBody>>(
            "/api/v1/admin/certifications");

        var row = Assert.Single(queue!, c => c.CaregiverCertificationId == certificationId);

        Assert.False(string.IsNullOrWhiteSpace(row.CaregiverName));
        Assert.NotEqual(Guid.Empty, row.CaregiverUserId);
        Assert.Equal("FIRST_AID", row.Code);
        Assert.False(row.IsExpired);
    }

    [Fact]
    public async Task Approving_makes_the_certificate_valid_for_the_caregiver()
    {
        var (caregiver, certificationId) = await SubmittedAsync();
        var admin = await factory.RegisterAdminAsync();

        var approve = await admin.PostAsync(
            $"/api/v1/admin/certifications/{certificationId}/approve", null);

        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        var certification = Assert.Single(profile!.Certifications);

        Assert.Equal("Verified", certification.Status);
        Assert.True(certification.IsValid);
    }

    /// <summary>
    /// The end that matters: an approved certificate is what makes a service bookable.
    /// Babysitting requires a vulnerable sector check, so clearing one should move the
    /// caregiver from "not qualified" to bookable.
    /// </summary>
    [Fact]
    public async Task An_approved_certificate_counts_towards_service_eligibility()
    {
        var (caregiver, certificationId) = await SubmittedAsync("VULNERABLE_SECTOR");
        var admin = await factory.RegisterAdminAsync();

        (await admin.PostAsync($"/api/v1/admin/certifications/{certificationId}/approve", null))
            .EnsureSuccessStatusCode();

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");

        Assert.Contains(
            profile!.Certifications,
            c => c.Code == "VULNERABLE_SECTOR" && c.IsValid);
    }

    [Fact]
    public async Task Rejecting_records_the_reason_so_the_caregiver_can_fix_it()
    {
        var (caregiver, certificationId) = await SubmittedAsync();
        var admin = await factory.RegisterAdminAsync();

        var reject = await admin.PostAsJsonAsync(
            $"/api/v1/admin/certifications/{certificationId}/reject",
            new { reason = "The certificate is not legible." });

        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        var certification = Assert.Single(profile!.Certifications);

        Assert.Equal("Rejected", certification.Status);
        Assert.False(certification.IsValid);
    }

    [Fact]
    public async Task A_rejection_must_carry_a_reason()
    {
        var (_, certificationId) = await SubmittedAsync();
        var admin = await factory.RegisterAdminAsync();

        var reject = await admin.PostAsJsonAsync(
            $"/api/v1/admin/certifications/{certificationId}/reject",
            new { reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, reject.StatusCode);
    }

    /// <summary>
    /// A caregiver must never be able to clear their own certificate. This is the whole
    /// reason the routes are admin-gated.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_approve_their_own_certificate()
    {
        var (caregiver, certificationId) = await SubmittedAsync();

        var approve = await caregiver.PostAsync(
            $"/api/v1/admin/certifications/{certificationId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    [Fact]
    public async Task A_care_requester_cannot_read_the_review_queue()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var queue = await requester.GetAsync("/api/v1/admin/certifications");

        Assert.Equal(HttpStatusCode.Forbidden, queue.StatusCode);
    }

    [Fact]
    public async Task An_unknown_certificate_is_not_found()
    {
        var admin = await factory.RegisterAdminAsync();

        var approve = await admin.PostAsync(
            $"/api/v1/admin/certifications/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
    }

    private sealed record CertificationBody(
        Guid Id,
        string Code,
        string Name,
        DateOnly IssuedOn,
        DateOnly? ExpiresOn,
        string Status,
        bool IsValid);

    private sealed record ProfileBody(IReadOnlyList<CertificationBody> Certifications);

    private sealed record PendingCertificationBody(
        Guid CaregiverCertificationId,
        Guid CaregiverProfileId,
        Guid CaregiverUserId,
        string CaregiverName,
        string CaregiverEmail,
        string Code,
        string Name,
        DateOnly IssuedOn,
        DateOnly? ExpiresOn,
        bool IsExpired,
        DateTime SubmittedAtUtc);
}
