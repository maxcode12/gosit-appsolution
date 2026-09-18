using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Administration;
using GoSit.Application.Features.Administration.CertificationReview;
using GoSit.Application.Features.Administration.Disputes;
using GoSit.Application.Features.Administration.InsuranceReview;
using GoSit.Application.Features.Administration.Moderation;
using GoSit.Application.Features.Administration.PromoCodes;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Reviews;
using GoSit.Application.Features.TrustAndSafety;
using GoSit.Domain.Growth;
using GoSit.Domain.Messaging;
using GoSit.Domain.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Staff operations.
///
/// The whole controller is behind the AdminOnly policy — these routes decide who is
/// allowed into a family's home and who is removed from the platform. Every action also
/// writes an audit row naming the staff member, so a decision can be traced afterwards.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdministrationController(
    IQueryHandler<ListPendingVerificationsQuery, IReadOnlyList<PendingVerification>> pendingVerifications,
    ICommandHandler<ApproveVerificationCommand, VerificationCaseResponse> approve,
    ICommandHandler<RejectVerificationCommand, VerificationCaseResponse> reject,
    IQueryHandler<ListOpenIncidentsQuery, IReadOnlyList<AdminIncident>> openIncidents,
    ICommandHandler<ResolveIncidentCommand, Unit> resolveIncident,
    ICommandHandler<SuspendAccountCommand, Unit> suspend,
    ICommandHandler<ReinstateAccountCommand, Unit> reinstate,
    IQueryHandler<ListPendingCertificationsQuery, IReadOnlyList<PendingCertification>> pendingCertifications,
    ICommandHandler<ApproveCertificationCommand, Unit> approveCertification,
    ICommandHandler<RejectCertificationCommand, Unit> rejectCertification,
    IQueryHandler<ListPromoCodesQuery, IReadOnlyList<AdminPromoCode>> promoCodes,
    ICommandHandler<CreatePromoCodeCommand, AdminPromoCode> createPromoCode,
    ICommandHandler<DeactivatePromoCodeCommand, AdminPromoCode> deactivatePromoCode,
    IQueryHandler<ReadCertificationDocumentQuery, StoredDocument> readCertificationDocument,
    IQueryHandler<ListPendingInsuranceQuery, IReadOnlyList<PendingInsurance>> pendingInsurance,
    ICommandHandler<ApproveInsuranceCommand, Unit> approveInsurance,
    ICommandHandler<RejectInsuranceCommand, Unit> rejectInsurance,
    IQueryHandler<ReadInsuranceDocumentQuery, StoredDocument> readInsuranceDocument,
    IQueryHandler<ListOpenDisputesQuery, IReadOnlyList<OpenDispute>> openDisputes,
    ICommandHandler<ResolveDisputeCommand, Unit> resolveDispute,
    IQueryHandler<ListFlaggedMessagesQuery, IReadOnlyList<FlaggedMessageRow>> flaggedMessages,
    IQueryHandler<ReadFlaggedMessageQuery, FlaggedMessageDetail> readFlaggedMessage,
    ICommandHandler<ModerateMessageCommand, Unit> moderateMessage,
    IQueryHandler<ListReportedReviewsQuery, IReadOnlyList<ReportedReviewRow>> reportedReviews,
    ICommandHandler<ModerateReviewCommand, Unit> moderateReview)
    : ControllerBase
{
    /// <summary>Reviews their subject has asked staff to look at, oldest first.</summary>
    [HttpGet("reported-reviews")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportedReviewRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportedReviewRow>>> ReportedReviews(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await reportedReviews.HandleAsync(new ListReportedReviewsQuery(take), cancellationToken));

    /// <summary>
    /// Takes a review down, or lets it stand.
    ///
    /// Hiding removes it from the profile and from the rating; it is never deleted or
    /// edited, because the review is evidence about its author as much as its subject.
    /// </summary>
    [HttpPost("reported-reviews/{reviewId:guid}/moderate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ModerateReview(
        Guid reviewId,
        [FromBody] ModerateReviewRequest request,
        [FromServices] IValidator<ModerateReviewCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ModerateReviewCommand(reviewId, request.Hide, request.Reason);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await moderateReview.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    [HttpGet("verifications")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingVerification>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingVerification>>> PendingVerifications(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await pendingVerifications.HandleAsync(
            new ListPendingVerificationsQuery(take), cancellationToken));

    /// <summary>
    /// Approves one check. The caregiver becomes verified only once every required
    /// check passes — approving a single check is not clearance to work.
    /// </summary>
    [HttpPost("verifications/{caseId:guid}/approve")]
    [ProducesResponseType(typeof(VerificationCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerificationCaseResponse>> Approve(
        Guid caseId,
        [FromBody] ApproveVerificationRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await approve.HandleAsync(
            new ApproveVerificationCommand(caseId, request?.ValidUntil, request?.Notes),
            cancellationToken));

    [HttpPost("verifications/{caseId:guid}/reject")]
    [ProducesResponseType(typeof(VerificationCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerificationCaseResponse>> Reject(
        Guid caseId,
        [FromBody] RejectVerificationRequest request,
        [FromServices] IValidator<RejectVerificationCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RejectVerificationCommand(caseId, request.Reason ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await reject.HandleAsync(command, cancellationToken));
    }

    /// <summary>Certificates waiting on a decision, oldest first.</summary>
    [HttpGet("certifications")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingCertification>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingCertification>>> PendingCertifications(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await pendingCertifications.HandleAsync(
            new ListPendingCertificationsQuery(take), cancellationToken));

    /// <summary>
    /// Clears one certificate. Until this happens the certificate confers no
    /// eligibility, so services that require it stay unbookable.
    /// </summary>
    [HttpPost("certifications/{caregiverCertificationId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveCertification(
        Guid caregiverCertificationId,
        CancellationToken cancellationToken)
    {
        await approveCertification.HandleAsync(
            new ApproveCertificationCommand(caregiverCertificationId), cancellationToken);

        return NoContent();
    }

    [HttpPost("certifications/{caregiverCertificationId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectCertification(
        Guid caregiverCertificationId,
        [FromBody] RejectCertificationRequest request,
        [FromServices] IValidator<RejectCertificationCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RejectCertificationCommand(
            caregiverCertificationId, request.Reason ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await rejectCertification.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// The certificate itself, so a decision is made by looking at something.
    ///
    /// Streamed through this endpoint rather than linked. There is no URL anywhere in Go
    /// Sit that resolves to a certification document — a link to a vulnerable-sector check
    /// works for whoever holds it, and links end up in browser histories and support
    /// tickets. Every read is audited against the staff member.
    /// </summary>
    [HttpGet("certifications/{caregiverCertificationId:guid}/document")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CertificationDocument(
        Guid caregiverCertificationId,
        CancellationToken cancellationToken)
    {
        var document = await readCertificationDocument.HandleAsync(
            new ReadCertificationDocumentQuery(caregiverCertificationId), cancellationToken);

        // As an attachment, never inline: an uploaded file must not be rendered in the
        // origin an admin is signed in to.
        return File(document.Content, document.ContentType, document.FileName);
    }

    /// <summary>Insurance policies waiting on a decision, oldest first.</summary>
    [HttpGet("insurance")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingInsurance>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingInsurance>>> PendingInsurance(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await pendingInsurance.HandleAsync(new ListPendingInsuranceQuery(take), cancellationToken));

    /// <summary>
    /// Clears one policy. GoSit is verifying the caregiver's own coverage here, never
    /// issuing or administering it — see docs/BUSINESS_MODEL.md.
    /// </summary>
    [HttpPost("insurance/{caregiverInsurancePolicyId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveInsurance(
        Guid caregiverInsurancePolicyId,
        CancellationToken cancellationToken)
    {
        await approveInsurance.HandleAsync(
            new ApproveInsuranceCommand(caregiverInsurancePolicyId), cancellationToken);

        return NoContent();
    }

    [HttpPost("insurance/{caregiverInsurancePolicyId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectInsurance(
        Guid caregiverInsurancePolicyId,
        [FromBody] RejectInsuranceRequest request,
        [FromServices] IValidator<RejectInsuranceCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RejectInsuranceCommand(caregiverInsurancePolicyId, request.Reason ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await rejectInsurance.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>The proof of insurance itself. Streamed, never linked — same reasoning as a certification.</summary>
    [HttpGet("insurance/{caregiverInsurancePolicyId:guid}/document")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InsuranceDocument(
        Guid caregiverInsurancePolicyId,
        CancellationToken cancellationToken)
    {
        var document = await readInsuranceDocument.HandleAsync(
            new ReadInsuranceDocumentQuery(caregiverInsurancePolicyId), cancellationToken);

        return File(document.Content, document.ContentType, document.FileName);
    }

    /// <summary>
    /// Disputed bookings nobody has settled, oldest first.
    ///
    /// Each one is holding a caregiver's payout. Until this existed a dispute was
    /// invisible to staff and the money it held had no way out.
    /// </summary>
    [HttpGet("disputes")]
    [ProducesResponseType(typeof(IReadOnlyList<OpenDispute>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpenDispute>>> Disputes(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await openDisputes.HandleAsync(new ListOpenDisputesQuery(take), cancellationToken));

    /// <summary>
    /// Settles a dispute by deciding what goes back to the family.
    ///
    /// A refund of zero is a real decision — the dispute was heard and dismissed, and the
    /// caregiver is paid. Everything else about the money follows from the amount.
    /// </summary>
    [HttpPost("disputes/{careBookingId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveDispute(
        Guid careBookingId,
        [FromBody] ResolveDisputeRequest request,
        [FromServices] IValidator<ResolveDisputeCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ResolveDisputeCommand(
            careBookingId, request.RefundAmount, request.Notes ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await resolveDispute.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>The triage queue: safety concerns and critical reports first.</summary>
    [HttpGet("incidents")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminIncident>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminIncident>>> Incidents(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await openIncidents.HandleAsync(new ListOpenIncidentsQuery(take), cancellationToken));

    [HttpPost("incidents/{reportId:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseIncident(
        Guid reportId,
        [FromBody] CloseIncidentRequest request,
        [FromServices] IValidator<ResolveIncidentCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new ResolveIncidentCommand(
            reportId, request.Notes ?? string.Empty, request.Dismiss);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await resolveIncident.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Messages flagged as possible contact sharing, waiting on a decision.
    ///
    /// Carries no message bodies. Seeing that something was flagged, between whom and
    /// when, is what the queue is for; reading it is a separate, audited request.
    /// </summary>
    [HttpGet("flagged-messages")]
    [ProducesResponseType(typeof(IReadOnlyList<FlaggedMessageRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FlaggedMessageRow>>> FlaggedMessages(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await flaggedMessages.HandleAsync(new ListFlaggedMessagesQuery(take), cancellationToken));

    /// <summary>
    /// Reads one flagged message.
    ///
    /// The only route in GoSit that returns a private message to someone who did not
    /// write or receive it. Every call writes an audit row naming the staff member, and
    /// the response is never cached.
    /// </summary>
    [HttpGet("flagged-messages/{messageId:guid}")]
    [ProducesResponseType(typeof(FlaggedMessageDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlaggedMessageDetail>> ReadFlaggedMessage(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var detail = await readFlaggedMessage.HandleAsync(
            new ReadFlaggedMessageQuery(messageId), cancellationToken);

        Response.Headers.CacheControl = "no-store, private";

        return Ok(detail);
    }

    [HttpPost("flagged-messages/{messageId:guid}/moderate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModerateMessage(
        Guid messageId,
        [FromBody] ModerateMessageRequest request,
        [FromServices] IValidator<ModerateMessageCommand> validator,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MessageModerationOutcome>(request.Outcome, out var outcome))
        {
            return BadRequest(new { error = "That is not a moderation outcome." });
        }

        var command = new ModerateMessageCommand(messageId, outcome, request.Notes);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await moderateMessage.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    /// <summary>Suspending signs the account out everywhere by revoking its tokens.</summary>
    [HttpPost("users/{userId:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(
        Guid userId,
        [FromBody] SuspendAccountRequest request,
        [FromServices] IValidator<SuspendAccountCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SuspendAccountCommand(userId, request.Reason ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await suspend.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    [HttpPost("users/{userId:guid}/reinstate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reinstate(Guid userId, CancellationToken cancellationToken)
    {
        await reinstate.HandleAsync(new ReinstateAccountCommand(userId), cancellationToken);
        return NoContent();
    }

    [HttpGet("promo-codes")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminPromoCode>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPromoCode>>> PromoCodes(
        CancellationToken cancellationToken) =>
        Ok(await promoCodes.HandleAsync(new ListPromoCodesQuery(), cancellationToken));

    [HttpPost("promo-codes")]
    [ProducesResponseType(typeof(AdminPromoCode), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminPromoCode>> CreatePromoCode(
        [FromBody] CreatePromoCodeRequest request,
        [FromServices] IValidator<CreatePromoCodeCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new CreatePromoCodeCommand(
            request.Code ?? string.Empty,
            request.Description ?? string.Empty,
            request.DiscountType,
            request.Value,
            request.ValidFromUtc,
            request.ValidUntilUtc,
            request.MaxRedemptions,
            request.Audience);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var created = await createPromoCode.HandleAsync(command, cancellationToken);

        return CreatedAtAction(nameof(PromoCodes), new { }, created);
    }

    /// <summary>
    /// Switches a code off. There is deliberately no edit and no delete — see
    /// <see cref="PromoCodeFeature"/> for why.
    /// </summary>
    [HttpPost("promo-codes/{promoCodeId:guid}/deactivate")]
    [ProducesResponseType(typeof(AdminPromoCode), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPromoCode>> DeactivatePromoCode(
        Guid promoCodeId,
        CancellationToken cancellationToken) =>
        Ok(await deactivatePromoCode.HandleAsync(
            new DeactivatePromoCodeCommand(promoCodeId), cancellationToken));
}

/// <summary>
/// What staff send to create a code. `ValidFromUtc` omitted means "start now".
/// </summary>
public sealed record CreatePromoCodeRequest(
    string? Code,
    string? Description,
    DiscountType DiscountType,
    decimal Value,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    int? MaxRedemptions,
    PlanAudience? Audience);

public sealed record ApproveVerificationRequest(DateOnly? ValidUntil, string? Notes);

public sealed record RejectVerificationRequest(string? Reason);

public sealed record CloseIncidentRequest(string? Notes, bool Dismiss);

/// <summary>A staff decision about a flagged message. Notes are required to escalate.</summary>
public sealed record ModerateMessageRequest(string Outcome, string? Notes);

/// <summary>A staff decision about a reported review. A reason is required to hide.</summary>
public sealed record ModerateReviewRequest(bool Hide, string? Reason);

public sealed record SuspendAccountRequest(string? Reason);

public sealed record RejectCertificationRequest(string? Reason);

public sealed record RejectInsuranceRequest(string? Reason);

/// <summary>
/// What staff send to settle a dispute. The amount is what goes back to the family;
/// zero dismisses the dispute and pays the caregiver.
/// </summary>
public sealed record ResolveDisputeRequest(decimal RefundAmount, string? Notes);
