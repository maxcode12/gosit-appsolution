using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Growth;
using GoSit.Application.Features.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Plans, subscriptions and referrals.
///
/// Everything is scoped to the caller's token — no route takes a user id, so one account
/// cannot read or change another's plan. Entitlements are always resolved server-side
/// from stored state; a client telling us what plan it is on is ignored.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class SubscriptionsController(
    IQueryHandler<ListPlansQuery, IReadOnlyList<PlanResponse>> listPlans,
    IQueryHandler<GetMySubscriptionQuery, MySubscriptionResponse> getMine,
    ICommandHandler<SubscribeCommand, SubscribeResult> subscribe,
    IQueryHandler<CreateBillingPortalLinkQuery, BillingPortalResponse> billingPortal,
    ICommandHandler<CancelSubscriptionCommand, MySubscriptionResponse> cancel,
    IQueryHandler<GetMyReferralsQuery, ReferralSummaryResponse> getReferrals,
    ICommandHandler<ApplyReferralCodeCommand, ReferralResponse> applyReferral) : ControllerBase
{
    /// <summary>Plans available to this account's side of the marketplace.</summary>
    [HttpGet("subscription-plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanResponse>>> Plans(
        CancellationToken cancellationToken) =>
        Ok(await listPlans.HandleAsync(new ListPlansQuery(), cancellationToken));

    /// <summary>
    /// What the caller is on. The returned feature list is authoritative — clients
    /// should gate on it rather than inferring from the plan code.
    /// </summary>
    [HttpGet("me/subscription")]
    [ProducesResponseType(typeof(MySubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MySubscriptionResponse>> Mine(CancellationToken cancellationToken) =>
        Ok(await getMine.HandleAsync(new GetMySubscriptionQuery(), cancellationToken));

    /// <summary>
    /// Starts a plan.
    ///
    /// With billing configured this does not grant anything — it returns a
    /// <c>checkoutUrl</c> to send the browser to, and the subscription becomes real only
    /// when the provider confirms payment over a verified webhook. With no billing
    /// configured the plan starts immediately and <c>subscription</c> is returned
    /// instead. A caller must handle both and must never treat a checkout URL as a
    /// completed subscription.
    /// </summary>
    [HttpPost("me/subscription")]
    [ProducesResponseType(typeof(SubscribeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubscribeResult>> Subscribe(
        [FromBody] SubscribeRequest request,
        [FromServices] IValidator<SubscribeCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SubscribeCommand(request.PlanCode ?? string.Empty, request.PromoCode);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await subscribe.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// A link to the provider's billing portal, where someone can change their card,
    /// read invoices and cancel.
    ///
    /// Not built in-house on purpose: doing so would mean handling card data and
    /// reimplementing invoices and tax receipts for no benefit.
    /// </summary>
    [HttpPost("me/billing-portal")]
    [ProducesResponseType(typeof(BillingPortalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BillingPortalResponse>> BillingPortal(
        CancellationToken cancellationToken) =>
        Ok(await billingPortal.HandleAsync(new CreateBillingPortalLinkQuery(), cancellationToken));

    /// <summary>Stops the renewal. Entitlements continue to the end of the paid period.</summary>
    [HttpDelete("me/subscription")]
    [ProducesResponseType(typeof(MySubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MySubscriptionResponse>> Cancel(CancellationToken cancellationToken) =>
        Ok(await cancel.HandleAsync(new CancelSubscriptionCommand(), cancellationToken));

    [HttpGet("me/referrals")]
    [ProducesResponseType(typeof(ReferralSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferralSummaryResponse>> Referrals(
        CancellationToken cancellationToken) =>
        Ok(await getReferrals.HandleAsync(new GetMyReferralsQuery(), cancellationToken));

    [HttpPost("me/referrals")]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReferralResponse>> ApplyReferral(
        [FromBody] ApplyReferralRequest request,
        [FromServices] IValidator<ApplyReferralCodeCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ApplyReferralCodeCommand(request.Code ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await applyReferral.HandleAsync(command, cancellationToken));
    }
}

public sealed record SubscribeRequest(string? PlanCode, string? PromoCode);

public sealed record ApplyReferralRequest(string? Code);
