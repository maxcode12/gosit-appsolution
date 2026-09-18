using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Reviews;
using GoSit.Application.Features.TrustAndSafety;
using GoSit.Domain.TrustAndSafety;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Reviews. Writing one is restricted to the two participants of a completed booking;
/// reading a caregiver's published reviews is open to any signed-in user, because that
/// is what a family uses to choose.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class ReviewsController(
    ICommandHandler<SubmitReviewCommand, ReviewResponse> submit,
    IQueryHandler<ListCaregiverReviewsQuery, CaregiverReviewsResponse> listForCaregiver,
    ICommandHandler<ReportReviewCommand, Unit> reportReview)
    : ControllerBase
{
    /// <summary>
    /// Asks staff to look at a review written about you.
    ///
    /// Only the subject may report, and only a published review — an unpublished one is
    /// invisible to its subject, and offering to report it would disclose it early and
    /// undo the blind-review rule that stops retaliation.
    /// </summary>
    [HttpPost("reviews/{reviewId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReportReview(
        Guid reviewId,
        [FromBody] ReportReviewRequest request,
        [FromServices] IValidator<ReportReviewCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ReportReviewCommand(reviewId, request.Reason ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await reportReview.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("bookings/{bookingId:guid}/review")]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewResponse>> Submit(
        Guid bookingId,
        [FromBody] SubmitReviewRequest request,
        [FromServices] IValidator<SubmitReviewCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SubmitReviewCommand(bookingId, request.Rating, request.Comment);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await submit.HandleAsync(command, cancellationToken));
    }

    [HttpGet("caregivers/{caregiverProfileId:guid}/reviews")]
    [ProducesResponseType(typeof(CaregiverReviewsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverReviewsResponse>> ListForCaregiver(
        Guid caregiverProfileId,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await listForCaregiver.HandleAsync(
            new ListCaregiverReviewsQuery(caregiverProfileId, take), cancellationToken));
}

/// <summary>
/// Reporting and caregiver verification.
///
/// Reporting is open to any signed-in user — anyone who sees something wrong must be
/// able to say so without first qualifying for a role.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class TrustAndSafetyController(
    ICommandHandler<ReportIncidentCommand, IncidentReportResponse> report,
    ICommandHandler<SubmitVerificationCommand, VerificationCaseResponse> submitVerification,
    IQueryHandler<ListMyVerificationsQuery, VerificationSummaryResponse> myVerifications)
    : ControllerBase
{
    [HttpPost("reports")]
    [ProducesResponseType(typeof(IncidentReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentReportResponse>> Report(
        [FromBody] ReportIncidentRequest request,
        [FromServices] IValidator<ReportIncidentCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ReportIncidentCommand(
            request.SubjectUserId,
            request.CareBookingId,
            request.Category,
            request.Severity,
            request.Description ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await report.HandleAsync(command, cancellationToken));
    }

    [HttpPost("caregivers/me/verifications")]
    [Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
    [ProducesResponseType(typeof(VerificationCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VerificationCaseResponse>> SubmitVerification(
        [FromBody] SubmitVerificationCommand command,
        [FromServices] IValidator<SubmitVerificationCommand> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await submitVerification.HandleAsync(command, cancellationToken));
    }

    [HttpGet("caregivers/me/verifications")]
    [Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
    [ProducesResponseType(typeof(VerificationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerificationSummaryResponse>> MyVerifications(
        CancellationToken cancellationToken) =>
        Ok(await myVerifications.HandleAsync(new ListMyVerificationsQuery(), cancellationToken));
}

public sealed record SubmitReviewRequest(int Rating, string? Comment);

public sealed record ReportIncidentRequest(
    Guid SubjectUserId,
    Guid? CareBookingId,
    IncidentCategory Category,
    IncidentSeverity Severity,
    string? Description);

public sealed record ReportReviewRequest(string? Reason);
