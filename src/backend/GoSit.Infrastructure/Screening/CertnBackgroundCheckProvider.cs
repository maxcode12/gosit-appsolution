using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.TrustAndSafety;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Screening;

public sealed class CertnOptions
{
    public const string SectionName = "Certn";

    /// <summary>
    /// Created in Certn's client portal under Settings, Integrations. Shown once, and expires
    /// after a year; more than one can be active, so replacing it needs no downtime.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Certn keeps each region's data in that region. Canada's address is the default; the
    /// testing environment has its own, which is what this is set to until Certn approves
    /// the account.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.ca.certn.co";

    /// <summary>
    /// Signs every notification Certn sends. Given when the notification address is added in
    /// the portal. Without it, notifications are refused rather than trusted.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public bool CanVerifyWebhooks => !string.IsNullOrWhiteSpace(WebhookSecret);
}

/// <summary>
/// Orders checks from Certn and asks how they are going.
///
/// Built from Certn's published API reference and not yet run against their service: no test
/// key existed when it was written. The first thing to do with one is order a test case in
/// Certn's sandbox; the request shapes here are the first place to look if Certn answers 400.
///
/// What this class decides, and what it leaves alone:
///  - It offers identity and criminal record checks, the two Certn's documentation shows.
///  - It does NOT offer the vulnerable sector check. Certn's older API lists one, but it has
///    not been confirmed in the current one, and guessing a check name would order the wrong
///    thing for someone who will care for children. That check stays with staff.
///  - Certn emails the caregiver directly, to give consent and supply their own details. The
///    only personal information sent is their email address.
/// </summary>
public sealed class CertnBackgroundCheckProvider(
    HttpClient client,
    IOptions<CertnOptions> options,
    ILogger<CertnBackgroundCheckProvider> logger) : IBackgroundCheckProvider
{
    public bool IsConfigured => options.Value.IsConfigured;

    public bool CanInitiate(VerificationCheckType checkType) =>
        checkType is VerificationCheckType.Identity or VerificationCheckType.BackgroundCheck;

    public async Task<string?> InitiateAsync(
        BackgroundCheckApplicant applicant,
        VerificationCheckType checkType,
        CancellationToken cancellationToken)
    {
        if (!CanInitiate(checkType))
        {
            return null;
        }

        var order = new JsonObject
        {
            ["email_address"] = applicant.Email,
            ["send_invite_email"] = true,
            ["check_types_with_arguments"] = ChecksFor(checkType),
        };

        using var response = await SendAsync(
            () => client.PostAsJsonAsync("/api/public/cases/order/", order, cancellationToken));

        if (!response.IsSuccessStatusCode)
        {
            // The status only. A response body can repeat the applicant's address back.
            logger.LogWarning(
                "Certn refused an order for {CheckType} (caregiver {CaregiverProfileId}): HTTP {Status}",
                checkType, applicant.CaregiverProfileId, (int)response.StatusCode);

            throw new ExternalServiceException(
                "Our screening provider could not start this check. Please try again shortly.");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var reference = body?["id"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(reference))
        {
            logger.LogWarning("Certn accepted an order but returned no case id");

            throw new ExternalServiceException(
                "Our screening provider could not start this check. Please try again shortly.");
        }

        return reference;
    }

    public async Task<BackgroundCheckOutcome?> GetOutcomeAsync(
        string providerReference,
        CancellationToken cancellationToken)
    {
        // The reference goes into a URL, so only an id of the shape Certn issues is let through.
        if (!Guid.TryParse(providerReference, out var caseId))
        {
            return null;
        }

        using var response = await SendAsync(
            () => client.GetAsync($"/api/public/cases/{caseId}/", cancellationToken));

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Certn could not report on a case: HTTP {Status}", (int)response.StatusCode);

            throw new ExternalServiceException(
                "Our screening provider is not answering. It will be retried.");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        var status = body?["overall_status"]?.GetValue<string>();

        return string.IsNullOrWhiteSpace(status)
            ? null
            : new BackgroundCheckOutcome(status, body?["overall_score"]?.GetValue<string>());
    }

    /// <summary>
    /// The check names are Certn's own, copied from the order examples in their API
    /// reference. Identity is verified by Certn against the documents the applicant
    /// supplies on their site.
    /// </summary>
    private static JsonObject ChecksFor(VerificationCheckType checkType) => checkType switch
    {
        VerificationCheckType.Identity => new JsonObject
        {
            ["IDENTITY_VERIFICATION_1"] = new JsonObject(),
        },
        VerificationCheckType.BackgroundCheck => new JsonObject
        {
            ["CRIMINAL_RECORD_REPORT_1"] = new JsonObject
            {
                ["ordering_type"] = "EXPLICIT_ORDERING",
                ["explicit_check_type"] = new JsonArray("BASIC_CANADIAN_CRIMINAL_RECORD_REPORT_1"),
            },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(checkType)),
    };

    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Certn could not be reached: {Reason}", ex.GetType().Name);

            throw new ExternalServiceException(
                "Our screening provider is not answering. Please try again shortly.");
        }
    }
}

public static class CertnHttpClientRegistration
{
    /// <summary>Applies the address, key and timeout. The timeout is short on purpose: Certn gives a notification ten seconds to be answered.</summary>
    public static void Configure(IServiceProvider services, HttpClient client)
    {
        var certn = services.GetRequiredService<IOptions<CertnOptions>>().Value;

        client.BaseAddress = new Uri(certn.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Key", certn.ApiKey);
    }
}
