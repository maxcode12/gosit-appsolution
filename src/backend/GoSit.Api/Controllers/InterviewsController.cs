using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GoSit.Api.Controllers;

/// <summary>
/// Video interviews between a family and a caregiver.
///
/// Used by both sides, like messaging: each handler works out which side the caller is on,
/// and an interview the caller is not part of is 404. Asking is for families only;
/// answering is for caregivers only.
/// </summary>
[ApiController]
[Route("api/v1/interviews")]
[Authorize]
public sealed class InterviewsController(
    ICommandHandler<RequestInterviewCommand, InterviewResponse> request,
    ICommandHandler<AcceptInterviewCommand, InterviewResponse> accept,
    ICommandHandler<DeclineInterviewCommand, InterviewResponse> decline,
    ICommandHandler<CancelInterviewCommand, InterviewResponse> cancel,
    ICommandHandler<JoinInterviewCommand, JoinInterviewResponse> join,
    IQueryHandler<ListInterviewsQuery, IReadOnlyList<InterviewResponse>> list,
    IQueryHandler<GetInterviewQuery, InterviewResponse> get) : ControllerBase
{
    /// <summary>The caller's interviews, newest first. <paramref name="withProfileId"/> narrows to one person.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InterviewResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InterviewResponse>>> List(
        [FromQuery] Guid? withProfileId,
        CancellationToken cancellationToken) =>
        Ok(await list.HandleAsync(new ListInterviewsQuery(withProfileId), cancellationToken));

    [HttpGet("{interviewId:guid}")]
    [ProducesResponseType(typeof(InterviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InterviewResponse>> Get(Guid interviewId, CancellationToken cancellationToken) =>
        Ok(await get.HandleAsync(new GetInterviewQuery(interviewId), cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
    [ProducesResponseType(typeof(InterviewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InterviewResponse>> Request(
        [FromBody] RequestInterviewRequest body,
        [FromServices] IValidator<RequestInterviewCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RequestInterviewCommand(body.CaregiverProfileId, body.ScheduledStartUtc, body.DurationMinutes);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var created = await request.HandleAsync(command, cancellationToken);

        return Created($"/api/v1/interviews/{created.Id}", created);
    }

    [HttpPost("{interviewId:guid}/accept")]
    [Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
    [ProducesResponseType(typeof(InterviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InterviewResponse>> Accept(Guid interviewId, CancellationToken cancellationToken) =>
        Ok(await accept.HandleAsync(new AcceptInterviewCommand(interviewId), cancellationToken));

    [HttpPost("{interviewId:guid}/decline")]
    [Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
    [ProducesResponseType(typeof(InterviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InterviewResponse>> Decline(Guid interviewId, CancellationToken cancellationToken) =>
        Ok(await decline.HandleAsync(new DeclineInterviewCommand(interviewId), cancellationToken));

    [HttpPost("{interviewId:guid}/cancel")]
    [ProducesResponseType(typeof(InterviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InterviewResponse>> Cancel(Guid interviewId, CancellationToken cancellationToken) =>
        Ok(await cancel.HandleAsync(new CancelInterviewCommand(interviewId), cancellationToken));

    /// <summary>
    /// Joins the call. Refused unless the caller agrees to the recording, and unless the
    /// recording is running by the time the token is issued. The token is never cached.
    /// </summary>
    [HttpPost("{interviewId:guid}/join")]
    [ProducesResponseType(typeof(JoinInterviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JoinInterviewResponse>> Join(
        Guid interviewId,
        [FromBody] JoinInterviewRequest body,
        CancellationToken cancellationToken)
    {
        var ticket = await join.HandleAsync(
            new JoinInterviewCommand(interviewId, body.ConsentToRecording), cancellationToken);

        Response.Headers.CacheControl = "no-store, private";

        return Ok(ticket);
    }
}

public sealed record RequestInterviewRequest(Guid CaregiverProfileId, DateTime ScheduledStartUtc, int DurationMinutes);

public sealed record JoinInterviewRequest(bool ConsentToRecording);

/// <summary>
/// Receives LiveKit's webhooks: recordings finishing or failing, and rooms closing.
///
/// Anonymous by necessity, like the payment webhook. The signature — a token signed with
/// the LiveKit API secret over a hash of the body — is verified before anything is applied,
/// and the body is read verbatim for that reason.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public sealed class VideoWebhookController(
    ICommandHandler<HandleVideoWebhookCommand, Unit> handleWebhook,
    ILogger<VideoWebhookController> logger) : ControllerBase
{
    private const int MaxPayloadBytes = 65_536;

    [HttpPost("livekit")]
    [EnableRateLimiting(RateLimitPolicies.Webhook)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LiveKit(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        if (payload.Length > MaxPayloadBytes)
        {
            logger.LogWarning("Rejected an oversized video webhook payload");
            return BadRequest();
        }

        try
        {
            await handleWebhook.HandleAsync(
                new HandleVideoWebhookCommand(payload, Request.Headers.Authorization.ToString()),
                cancellationToken);
        }
        catch (WebhookVerificationException ex)
        {
            logger.LogWarning("Rejected a video webhook: {Reason}", ex.Message);
            return BadRequest();
        }

        return Ok();
    }
}
