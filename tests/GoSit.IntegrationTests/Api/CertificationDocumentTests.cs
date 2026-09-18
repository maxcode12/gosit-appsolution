using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GoSit.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Uploading certification evidence, and who can read it.
///
/// The hole this closes: staff were approving certifications — the thing that lets a
/// caregiver work with children and vulnerable adults — from a code and two dates the
/// caregiver had typed. There was no upload, and the queue did not even show whether a
/// document existed.
///
/// The tests that matter most here are about who can read the file afterwards. What is in
/// it is a criminal record check, and CLAUDE.md forbids exposing one.
/// </summary>
[Collection(nameof(ApiCollection))]
public class CertificationDocumentTests(GoSitApiFactory factory)
{
    private sealed record CertificationBody(Guid Id, string Status);

    private sealed record ProfileBody(IReadOnlyList<CertificationBody> Certifications);

    private sealed record PendingBody(
        Guid CaregiverCertificationId, string Code, bool HasDocument);

    /// <summary>A caregiver whose profile exists, so a certification can hang off it.</summary>
    private async Task<HttpClient> CaregiverWithProfileAsync()
    {
        var client = await factory.RegisterCaregiverAsync();

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ten years with children.",
            yearsOfExperience = 10,
            hourlyRate = 28m,
            serviceRadiusKm = 15,
            address = new
            {
                line1 = "12 Bloor St W",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5S 1M2",
                country = "CA",
            },
        });

        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>A caregiver with one submitted, undocumented certification.</summary>
    private async Task<(HttpClient Client, Guid CertificationId)> CaregiverWithCertificationAsync()
    {
        var client = await CaregiverWithProfileAsync();

        var response = await client.PostAsJsonAsync("/api/v1/caregivers/me/certifications", new
        {
            certificationCode = "VULNERABLE_SECTOR",
            issuedOn = "2026-01-10",
            expiresOn = "2028-01-10",
        });

        response.EnsureSuccessStatusCode();

        var submitted = await response.Content.ReadFromJsonAsync<CertificationBody>();

        return (client, submitted!.Id);
    }

    /// <summary>A small PNG. Content matters only in that it comes back byte for byte.</summary>
    private static MultipartFormDataContent FileContent(
        string fileName = "certificate.png",
        string contentType = "image/png",
        string body = "not-really-a-png-but-the-bytes-round-trip")
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client, Guid certificationId, MultipartFormDataContent content) =>
        client.PostAsync(
            $"/api/v1/caregivers/me/certifications/{certificationId}/document", content);

    // ---- the gate ----

    /// <summary>
    /// The rule the whole feature exists for. Approving without evidence is approving a
    /// caregiver's own claim about themselves.
    /// </summary>
    [Fact]
    public async Task Staff_cannot_approve_a_certification_with_no_document()
    {
        var (_, certificationId) = await CaregiverWithCertificationAsync();
        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsync(
            $"/api/v1/admin/certifications/{certificationId}/approve", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Staff_can_approve_once_a_document_is_uploaded()
    {
        var (caregiver, certificationId) = await CaregiverWithCertificationAsync();

        (await UploadAsync(caregiver, certificationId, FileContent())).EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        var response = await admin.PostAsync(
            $"/api/v1/admin/certifications/{certificationId}/approve", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// The queue has to say whether there is anything to look at, or a reviewer meets the
    /// refusal above with no idea why.
    /// </summary>
    [Fact]
    public async Task The_review_queue_reports_whether_a_document_exists()
    {
        var (caregiver, withDocument) = await CaregiverWithCertificationAsync();
        var (_, without) = await CaregiverWithCertificationAsync();

        (await UploadAsync(caregiver, withDocument, FileContent())).EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        var pending = await admin.GetFromJsonAsync<List<PendingBody>>(
            "/api/v1/admin/certifications?take=200");

        Assert.True(pending!.Single(c => c.CaregiverCertificationId == withDocument).HasDocument);
        Assert.False(pending.Single(c => c.CaregiverCertificationId == without).HasDocument);
    }

    // ---- who can read it ----

    /// <summary>
    /// A criminal record check must not be readable by another caregiver, however they
    /// come by the id.
    /// </summary>
    [Fact]
    public async Task A_caregiver_cannot_read_another_caregivers_document()
    {
        var (owner, certificationId) = await CaregiverWithCertificationAsync();
        (await UploadAsync(owner, certificationId, FileContent())).EnsureSuccessStatusCode();

        var stranger = await factory.RegisterCaregiverAsync();

        var response = await stranger.GetAsync(
            $"/api/v1/admin/certifications/{certificationId}/document");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_care_requester_cannot_read_a_certification_document()
    {
        var (owner, certificationId) = await CaregiverWithCertificationAsync();
        (await UploadAsync(owner, certificationId, FileContent())).EnsureSuccessStatusCode();

        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.GetAsync(
            $"/api/v1/admin/certifications/{certificationId}/document");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_document_requires_authentication()
    {
        var (owner, certificationId) = await CaregiverWithCertificationAsync();
        (await UploadAsync(owner, certificationId, FileContent())).EnsureSuccessStatusCode();

        var response = await factory.CreateClient().GetAsync(
            $"/api/v1/admin/certifications/{certificationId}/document");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Staff get the bytes back, as an attachment. Inline would let an uploaded file
    /// render in the origin an admin is signed in to.
    /// </summary>
    [Fact]
    public async Task Staff_can_read_the_uploaded_document()
    {
        var (caregiver, certificationId) = await CaregiverWithCertificationAsync();

        (await UploadAsync(caregiver, certificationId, FileContent(body: "the-certificate")))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        var response = await admin.GetAsync(
            $"/api/v1/admin/certifications/{certificationId}/document");

        response.EnsureSuccessStatusCode();

        Assert.Equal("the-certificate", await response.Content.ReadAsStringAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    // ---- what may be uploaded ----

    [Fact]
    public async Task Uploading_to_another_caregivers_certification_is_not_found()
    {
        var (_, certificationId) = await CaregiverWithCertificationAsync();

        var stranger = await CaregiverWithProfileAsync();

        var response = await UploadAsync(stranger, certificationId, FileContent());

        // Not found rather than forbidden: a 403 would confirm the id exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// An allow-list, because staff open these. An SVG is a document to a file picker and
    /// a script to a browser.
    /// </summary>
    [Theory]
    [InlineData("image/svg+xml", "certificate.svg")]
    [InlineData("text/html", "certificate.html")]
    [InlineData("application/zip", "certificate.zip")]
    public async Task An_unsupported_file_type_is_refused(string contentType, string fileName)
    {
        var (caregiver, certificationId) = await CaregiverWithCertificationAsync();

        var response = await UploadAsync(
            caregiver, certificationId, FileContent(fileName, contentType));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Replacing the evidence undoes the approval it earned. Swapping a blurry photo is
    /// ordinary and innocent, which is exactly why it cannot be allowed to keep a badge
    /// granted against the previous file.
    /// </summary>
    [Fact]
    public async Task Replacing_the_document_sends_the_certification_back_for_review()
    {
        var (caregiver, certificationId) = await CaregiverWithCertificationAsync();

        (await UploadAsync(caregiver, certificationId, FileContent(body: "first")))
            .EnsureSuccessStatusCode();

        var admin = await factory.RegisterAdminAsync();

        (await admin.PostAsync($"/api/v1/admin/certifications/{certificationId}/approve", null))
            .EnsureSuccessStatusCode();

        (await UploadAsync(caregiver, certificationId, FileContent(body: "second")))
            .EnsureSuccessStatusCode();

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");

        Assert.Equal(
            "Pending",
            profile!.Certifications.Single(c => c.Id == certificationId).Status);

        // And staff now see the replacement, not the file they approved.
        var document = await admin.GetAsync(
            $"/api/v1/admin/certifications/{certificationId}/document");

        Assert.Equal("second", await document.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// The storage is real in this suite — Azurite over the same client the deployment
    /// uses. A stubbed store would prove the handler calls it and nothing about whether
    /// a blob round-trips.
    /// </summary>
    [Fact]
    public void The_document_store_is_configured()
    {
        using var scope = factory.Services.CreateScope();

        Assert.True(scope.ServiceProvider.GetRequiredService<IDocumentStore>().IsLive);
    }
}
