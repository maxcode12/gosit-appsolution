using System.Text.Json;
using System.Text.RegularExpressions;
using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.TrustAndSafety;
using GoSit.Infrastructure.Screening;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GoSit.Api.Controllers;

/// <summary>
/// Where Certn tells us a background check has moved.
///
/// Anonymous by necessity, like the payment webhook: Certn cannot present a GoSit token. What
/// protects it is the signature on every message, checked before anything in the body is read
/// as meaning anything. An unsigned or wrongly signed message is refused and changes nothing.
///
/// A message only names the case. What the case now says is asked of Certn directly, so a
/// repeated message is harmless and one delivered late cannot undo a newer one. Certn's
/// retries make failing loudly the right behaviour: if we cannot reach them, answering with
/// an error brings the message back.
/// </summary>
[ApiController]
[Route("api/v1/webhooks/certn")]
[AllowAnonymous]
public sealed partial class CertnWebhookController(
    IOptions<CertnOptions> options,
    ICommandHandler<ApplyProviderUpdateCommand, Unit> applyUpdate,
    ILogger<CertnWebhookController> logger) : ControllerBase
{
    private const int MaxPayloadBytes = 65_536;

    private static readonly HashSet<string> CaseEvents =
    [
        "CASE_STATUS_CHANGED",
        "CHECK_STATUS_CHANGED",
        "CASE_REPORT_READY",
    ];

    [GeneratedRegex("^[A-Za-z0-9._~-]{1,200}$")]
    private static partial Regex SafeChallenge();

    /// <summary>
    /// Certn proves we own this address by sending a random value and expecting it straight
    /// back, within ten seconds. If this fails, Certn switches delivery off.
    ///
    /// Echoing text back to anyone is only safe if it cannot be mistaken for a page, so the
    /// answer is plain text with sniffing disabled, and only a plain token is ever repeated.
    /// </summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Webhook)]
    public IActionResult Verify([FromQuery] string? challenge)
    {
        if (string.IsNullOrEmpty(challenge) || !SafeChallenge().IsMatch(challenge))
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        return Content(challenge, "text/plain");
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Webhook)]
    [RequestSizeLimit(MaxPayloadBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // No signing key, no way to tell a real message from a forged one. Refuse them all.
        if (!options.Value.CanVerifyWebhooks)
        {
            logger.LogWarning("A Certn message arrived but no signing secret is configured");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.ToArray();

        if (!CertnWebhookSignature.IsValid(body, Request.Headers["X-Signature"].ToString(), options.Value.WebhookSecret))
        {
            logger.LogWarning("Rejected a Certn message with a missing or wrong signature");
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        string? eventType;
        string? objectType;
        string? objectId;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            eventType = Read(root, "event_type");
            objectType = Read(root, "object_type");
            objectId = Read(root, "object_id");
        }
        catch (JsonException)
        {
            // Signed, but not something we understand. Acknowledge so it is not retried for hours.
            logger.LogWarning("A signed Certn message was not valid JSON");
            return Ok();
        }

        if (eventType is not null
            && CaseEvents.Contains(eventType)
            && objectType == "CASE"
            && !string.IsNullOrWhiteSpace(objectId))
        {
            await applyUpdate.HandleAsync(new ApplyProviderUpdateCommand(objectId), cancellationToken);
        }

        return Ok();
    }

    private static string? Read(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
