using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Payments;
using GoSit.Application.Features.Payments.CreatePaymentIntent;
using GoSit.Application.Features.Payments.GetReceipt;
using GoSit.Application.Features.Payments.HandleWebhook;
using GoSit.Application.Features.Payouts;
using GoSit.Application.Features.Payouts.GetEarnings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GoSit.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class PaymentsController(
    ICommandHandler<CreatePaymentIntentCommand, PaymentIntentResponse> createIntent,
    IQueryHandler<GetPaymentReceiptQuery, PaymentReceiptResponse> getReceipt,
    IQueryHandler<GetEarningsQuery, EarningsResponse> getEarnings,
    IQueryHandler<GetPayoutAccountQuery, PayoutAccountResponse> getPayoutAccount,
    ICommandHandler<StartPayoutOnboardingCommand, PayoutOnboardingResponse> startPayoutOnboarding)
    : ControllerBase
{
    /// <summary>
    /// Starts payment for a booking.
    ///
    /// The request body carries a booking id only. The amount is derived server-side
    /// from the booking, so a client cannot influence what it is charged.
    /// </summary>
    [HttpPost("bookings/{bookingId:guid}/payment-intent")]
    [Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
    [ProducesResponseType(typeof(PaymentIntentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentIntentResponse>> CreateIntent(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        Ok(await createIntent.HandleAsync(new CreatePaymentIntentCommand(bookingId), cancellationToken));

    /// <summary>The receipt for a booking. Visible to both participants.</summary>
    [HttpGet("bookings/{bookingId:guid}/payment")]
    [ProducesResponseType(typeof(PaymentReceiptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentReceiptResponse>> GetReceipt(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        Ok(await getReceipt.HandleAsync(new GetPaymentReceiptQuery(bookingId), cancellationToken));

    /// <summary>
    /// Where the calling caregiver stands with payouts.
    ///
    /// Distinguishes "not started", "submitted and under review" and "ready", because
    /// those call for different things to be said to someone.
    /// </summary>
    [HttpGet("caregivers/me/payout-account")]
    [Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
    [ProducesResponseType(typeof(PayoutAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayoutAccountResponse>> PayoutAccount(
        CancellationToken cancellationToken) =>
        Ok(await getPayoutAccount.HandleAsync(new GetPayoutAccountQuery(), cancellationToken));

    /// <summary>
    /// Starts, or resumes, hosted payout onboarding and returns where to send the
    /// browser.
    ///
    /// GoSit never collects bank details — they go to the provider directly. The link
    /// is single-use and short-lived, so this is called again rather than the URL being
    /// stored.
    /// </summary>
    [HttpPost("caregivers/me/payout-account")]
    [Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
    [ProducesResponseType(typeof(PayoutOnboardingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayoutOnboardingResponse>> StartPayoutOnboarding(
        CancellationToken cancellationToken) =>
        Ok(await startPayoutOnboarding.HandleAsync(
            new StartPayoutOnboardingCommand(), cancellationToken));

    /// <summary>The calling caregiver's wallet and statement.</summary>
    [HttpGet("caregivers/me/earnings")]
    [Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
    [ProducesResponseType(typeof(EarningsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EarningsResponse>> GetEarnings(CancellationToken cancellationToken) =>
        Ok(await getEarnings.HandleAsync(new GetEarningsQuery(), cancellationToken));
}

/// <summary>
/// Receives provider webhooks.
///
/// Anonymous by necessity — Stripe cannot present a GoSit token. Authentication is the
/// signature on the payload instead, verified before anything is applied. An unsigned or
/// wrongly-signed request is rejected with 400 and changes nothing.
///
/// The raw body is read verbatim: signature verification is over the exact bytes sent,
/// so the payload must not be model-bound and re-serialized first.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public sealed class PaymentWebhookController(
    ICommandHandler<HandlePaymentWebhookCommand, Unit> handleWebhook,
    ILogger<PaymentWebhookController> logger) : ControllerBase
{
    /// <summary>Stripe's own cap on webhook payload size.</summary>
    private const int MaxPayloadBytes = 65_536;

    [HttpPost("stripe")]
    [EnableRateLimiting(RateLimitPolicies.Webhook)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Stripe(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        if (payload.Length > MaxPayloadBytes)
        {
            logger.LogWarning("Rejected an oversized webhook payload");
            return BadRequest();
        }

        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await handleWebhook.HandleAsync(
                new HandlePaymentWebhookCommand(payload, signature), cancellationToken);
        }
        catch (WebhookVerificationException ex)
        {
            // Logged without the payload: an unverified body is attacker-controlled and
            // has no business in the logs.
            logger.LogWarning("Rejected a webhook: {Reason}", ex.Message);
            return BadRequest();
        }

        return Ok();
    }
}
