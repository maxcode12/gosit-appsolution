using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Administration.Interviews;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Interview recordings, for staff.
///
/// The only place a recording can be watched. Listing shows who and when; watching is a
/// separate request audited against the staff member. Holding keeps recordings past their
/// deletion date for a dispute or incident.
/// </summary>
[ApiController]
[Route("api/v1/admin/interviews")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminInterviewsController(
    IQueryHandler<ListRecordedInterviewsQuery, IReadOnlyList<AdminInterview>> list,
    IQueryHandler<ReadInterviewRecordingQuery, StoredDocument> read,
    ICommandHandler<HoldInterviewRecordingsCommand, Unit> hold,
    ICommandHandler<ReleaseInterviewRecordingsCommand, Unit> release) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminInterview>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminInterview>>> List(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await list.HandleAsync(new ListRecordedInterviewsQuery(take), cancellationToken));

    [HttpGet("recordings/{recordingId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Watch(Guid recordingId, CancellationToken cancellationToken)
    {
        var recording = await read.HandleAsync(new ReadInterviewRecordingQuery(recordingId), cancellationToken);

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(recording.Content, recording.ContentType);
    }

    [HttpPost("{interviewId:guid}/hold")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Hold(
        Guid interviewId,
        [FromBody] HoldInterviewRecordingsRequest body,
        [FromServices] IValidator<HoldInterviewRecordingsCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new HoldInterviewRecordingsCommand(interviewId, body.Reason ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await hold.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("{interviewId:guid}/release")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release(Guid interviewId, CancellationToken cancellationToken)
    {
        await release.HandleAsync(new ReleaseInterviewRecordingsCommand(interviewId), cancellationToken);
        return NoContent();
    }
}

public sealed record HoldInterviewRecordingsRequest(string? Reason);
