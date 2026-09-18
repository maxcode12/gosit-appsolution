using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Electronically signing the Caregiver Service Agreement.
///
/// A caregiver cannot be listed without a current signature — see
/// <c>CaregiverProfile.IsListable</c> — and the version signed is always the server's,
/// never one the caller names, so a version bump (once the draft agreement is
/// lawyer-approved) cannot be dodged by resubmitting an old one.
/// </summary>
[Collection(nameof(ApiCollection))]
public class ServiceAgreementTests(GoSitApiFactory factory)
{
    private sealed record ProfileBody(
        string? AgreementAcceptedVersion,
        DateTime? AgreementAcceptedAtUtc,
        bool HasAcceptedCurrentAgreement,
        string CurrentAgreementVersion,
        bool IsListable);

    /// <summary>A caregiver whose profile exists — signing and insurance both require one.</summary>
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

    [Fact]
    public async Task A_new_caregiver_has_not_signed_anything()
    {
        var caregiver = await CaregiverAsync();

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");

        Assert.Null(profile!.AgreementAcceptedVersion);
        Assert.Null(profile.AgreementAcceptedAtUtc);
        Assert.False(profile.HasAcceptedCurrentAgreement);
        Assert.False(string.IsNullOrWhiteSpace(profile.CurrentAgreementVersion));
    }

    [Fact]
    public async Task Signing_records_the_current_version_and_a_timestamp()
    {
        var caregiver = await CaregiverAsync();

        var response = await caregiver.PostAsJsonAsync(
            "/api/v1/caregivers/me/service-agreement/accept", new { signedName = "Ada Lovelace" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var accepted = await response.Content.ReadFromJsonAsync<ProfileBody>();

        Assert.Equal(accepted!.CurrentAgreementVersion, accepted.AgreementAcceptedVersion);
        Assert.NotNull(accepted.AgreementAcceptedAtUtc);
        Assert.True(accepted.HasAcceptedCurrentAgreement);
    }

    [Fact]
    public async Task An_empty_signed_name_is_refused()
    {
        var caregiver = await CaregiverAsync();

        var response = await caregiver.PostAsJsonAsync(
            "/api/v1/caregivers/me/service-agreement/accept", new { signedName = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Signing_requires_authentication()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/caregivers/me/service-agreement/accept", new { signedName = "Ada Lovelace" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The end that matters: an otherwise-complete profile still cannot be listed until
    /// it is signed, and becomes listable the moment it is.
    /// </summary>
    [Fact]
    public async Task An_unsigned_caregiver_is_not_listable_even_with_an_otherwise_complete_profile()
    {
        var caregiver = await CaregiverAsync();

        (await caregiver.PutAsJsonAsync("/api/v1/caregivers/me", new
        {
            bio = "Ten years with children.",
            yearsOfExperience = 10,
            hourlyRate = 28m,
            address = new
            {
                line1 = "12 Bloor St W",
                line2 = (string?)null,
                city = "Toronto",
                province = "ON",
                postalCode = "M5S 1M2",
                country = "CA",
            },
            serviceRadiusKm = 20,
        })).EnsureSuccessStatusCode();

        (await caregiver.PutAsJsonAsync("/api/v1/caregivers/me/availability", new
        {
            windows = new[] { new { day = "Monday", startTime = "08:00:00", endTime = "20:00:00" } },
        })).EnsureSuccessStatusCode();

        var profile = await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me");
        Assert.False(profile!.IsListable);

        (await caregiver.PostAsJsonAsync(
            "/api/v1/caregivers/me/service-agreement/accept", new { signedName = "Ada Lovelace" }))
            .EnsureSuccessStatusCode();

        // Verification and accepting-work still stand between here and listable — signing
        // only removes the one gap this feature is responsible for.
        Assert.False((await caregiver.GetFromJsonAsync<ProfileBody>("/api/v1/caregivers/me"))!.IsListable);
    }
}
