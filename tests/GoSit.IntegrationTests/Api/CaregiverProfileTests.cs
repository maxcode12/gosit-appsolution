using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace GoSit.IntegrationTests.Api;

[Collection(nameof(ApiCollection))]
public class CaregiverProfileTests(GoSitApiFactory factory)
{
    private sealed record CaregiverBody(
        Guid Id,
        string? Bio,
        int YearsOfExperience,
        decimal HourlyRate,
        object? Address,
        int ServiceRadiusKm,
        string VerificationStatus,
        bool AcceptingWork,
        bool IsListable,
        IReadOnlyList<AvailabilityBody> Availability,
        IReadOnlyList<CertificationBody> Certifications);

    private sealed record AvailabilityBody(string Day, TimeOnly StartTime, TimeOnly EndTime);

    private sealed record CertificationBody(
        Guid Id, string Code, string Name, DateOnly IssuedOn,
        DateOnly? ExpiresOn, string Status, bool IsValid);

    private static object ProfilePayload(decimal hourlyRate = 25m) => new
    {
        bio = "Five years with school-age children and senior companionship.",
        yearsOfExperience = 5,
        hourlyRate,
        address = new
        {
            line1 = "88 Queen St E",
            line2 = (string?)null,
            city = "Toronto",
            province = "ON",
            postalCode = "M5C 1S6",
            country = "CA",
        },
        serviceRadiusKm = 15,
    };

    private static async Task<HttpClient> CaregiverWithProfileAsync(GoSitApiFactory factory)
    {
        var client = await factory.RegisterCaregiverAsync();
        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me", ProfilePayload());
        response.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task A_caregiver_can_create_and_read_their_profile()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var profile = await client.GetFromJsonAsync<CaregiverBody>("/api/v1/caregivers/me");

        Assert.NotNull(profile);
        Assert.Equal(25m, profile.HourlyRate);
        Assert.Equal(5, profile.YearsOfExperience);
        Assert.Equal(15, profile.ServiceRadiusKm);
    }

    /// <summary>
    /// A caregiver must not be able to make themselves verified or listed by writing
    /// their own profile. Verification is Trust and Safety's decision alone.
    /// </summary>
    [Fact]
    public async Task A_new_caregiver_is_unverified_and_not_listable()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var profile = await client.GetFromJsonAsync<CaregiverBody>("/api/v1/caregivers/me");

        Assert.Equal("Unverified", profile!.VerificationStatus);
        Assert.False(profile.AcceptingWork);
        Assert.False(profile.IsListable);
    }

    [Fact]
    public async Task A_rate_below_minimum_wage_is_rejected()
    {
        var client = await factory.RegisterCaregiverAsync();

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me", ProfilePayload(hourlyRate: 5m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_care_requester_cannot_use_the_caregiver_endpoints()
    {
        var requester = await factory.RegisterCareRequesterAsync();

        var response = await requester.GetAsync("/api/v1/caregivers/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Availability_can_be_set_and_read_back()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me/availability", new
        {
            windows = new[]
            {
                new { day = "Monday", startTime = "09:00:00", endTime = "17:00:00" },
                new { day = "Tuesday", startTime = "09:00:00", endTime = "12:00:00" },
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<CaregiverBody>();
        Assert.Equal(2, profile!.Availability.Count);
    }

    [Fact]
    public async Task Overlapping_availability_is_rejected()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me/availability", new
        {
            windows = new[]
            {
                new { day = "Monday", startTime = "09:00:00", endTime = "13:00:00" },
                new { day = "Monday", startTime = "12:00:00", endTime = "17:00:00" },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A window ending earlier in the day than it starts runs past midnight. This test
    /// asserted the opposite until overnight care became bookable — the rule it protected
    /// was what made a service GoSit sells impossible to buy.
    /// </summary>
    [Fact]
    public async Task A_window_that_ends_before_it_starts_runs_past_midnight()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me/availability", new
        {
            windows = new[] { new { day = "Friday", startTime = "20:00:00", endTime = "07:00:00" } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Equal times stay refused: they could mean a full day or none at all.</summary>
    [Fact]
    public async Task A_window_with_equal_start_and_end_is_rejected()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me/availability", new
        {
            windows = new[] { new { day = "Monday", startTime = "09:00:00", endTime = "09:00:00" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_submitted_certification_is_pending_and_confers_nothing()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/caregivers/me/certifications", new
        {
            certificationCode = "CPR",
            issuedOn = "2026-01-10",
            expiresOn = "2028-01-10",
            documentReference = "blob/cert-123",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var certification = await response.Content.ReadFromJsonAsync<CertificationBody>();
        Assert.Equal("CPR", certification!.Code);
        Assert.Equal("Pending", certification.Status);
        Assert.False(certification.IsValid);
    }

    /// <summary>
    /// The uploaded document key is a private Blob Storage reference and must not
    /// appear in any response.
    /// </summary>
    [Fact]
    public async Task The_certification_document_reference_is_never_returned()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var submit = await client.PostAsJsonAsync("/api/v1/caregivers/me/certifications", new
        {
            certificationCode = "CPR",
            issuedOn = "2026-01-10",
            expiresOn = "2028-01-10",
            documentReference = "blob/super-secret-key",
        });

        Assert.DoesNotContain("super-secret-key", await submit.Content.ReadAsStringAsync());

        var profile = await client.GetStringAsync("/api/v1/caregivers/me");
        Assert.DoesNotContain("super-secret-key", profile);
        Assert.DoesNotContain("documentReference", profile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unknown_certification_code_is_rejected()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/caregivers/me/certifications", new
        {
            certificationCode = "NOT_A_REAL_CERTIFICATION",
            issuedOn = "2026-01-10",
            expiresOn = "2028-01-10",
            documentReference = (string?)null,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_certification_requiring_expiry_cannot_omit_one()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/caregivers/me/certifications", new
        {
            certificationCode = "CPR",
            issuedOn = "2026-01-10",
            expiresOn = (string?)null,
            documentReference = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_already_expired_certification_is_rejected()
    {
        var client = await CaregiverWithProfileAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/caregivers/me/certifications", new
        {
            certificationCode = "FIRST_AID",
            issuedOn = "2020-01-10",
            expiresOn = "2022-01-10",
            documentReference = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Setting_availability_without_a_profile_returns_404()
    {
        var client = await factory.RegisterCaregiverAsync();

        var response = await client.PutAsJsonAsync("/api/v1/caregivers/me/availability", new
        {
            windows = new[] { new { day = "Monday", startTime = "09:00:00", endTime = "17:00:00" } },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
