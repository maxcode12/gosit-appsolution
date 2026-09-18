using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Caregivers;
using GoSit.Application.Features.Caregivers.AcceptServiceAgreement;
using GoSit.Application.Features.Caregivers.GetInsights;
using GoSit.Application.Features.Caregivers.GetProfile;
using GoSit.Application.Features.Caregivers.SetAcceptingWork;
using GoSit.Application.Features.Caregivers.SetAvailability;
using GoSit.Application.Features.Caregivers.SetAvailableForLastMinuteRequests;
using GoSit.Application.Features.Caregivers.SetServices;
using GoSit.Application.Features.Caregivers.SubmitCertification;
using GoSit.Application.Features.Caregivers.SubmitInsurance;
using GoSit.Application.Features.Caregivers.UpsertProfile;
using GoSit.Domain.Caregivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// The caller's own caregiver profile. As with care requesters, there is no route
/// that takes a profile id — the profile is always resolved from the access token.
///
/// Verification status is read-only here. Only Trust and Safety may change it, so it
/// gets an admin endpoint in Phase 8 rather than a field on this payload.
/// </summary>
[ApiController]
[Route("api/v1/caregivers/me")]
[Authorize(Policy = AuthorizationPolicies.CaregiverOnly)]
public sealed class CaregiverProfileController(
    ICommandHandler<UpsertCaregiverProfileCommand, CaregiverProfileResponse> upsert,
    ICommandHandler<SetAvailabilityCommand, CaregiverProfileResponse> setAvailability,
    ICommandHandler<SetAcceptingWorkCommand, CaregiverProfileResponse> setAcceptingWork,
    ICommandHandler<SetAvailableForLastMinuteRequestsCommand, CaregiverProfileResponse> setAvailableForLastMinuteRequests,
    ICommandHandler<SubmitCertificationCommand, CaregiverCertificationResponse> submitCertification,
    ICommandHandler<SetServicesCommand, CaregiverProfileResponse> setServices,
    IQueryHandler<GetCaregiverProfileQuery, CaregiverProfileResponse> getProfile,
    IQueryHandler<GetInsightsQuery, CaregiverInsightsResponse> getInsights,
    ICommandHandler<UploadCertificationDocumentCommand, Unit> uploadDocument,
    ICommandHandler<AcceptServiceAgreementCommand, CaregiverProfileResponse> acceptServiceAgreement,
    ICommandHandler<SubmitInsuranceCommand, CaregiverInsurancePolicyResponse> submitInsurance,
    ICommandHandler<UploadInsuranceDocumentCommand, Unit> uploadInsuranceDocument) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CaregiverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverProfileResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await getProfile.HandleAsync(new GetCaregiverProfileQuery(), cancellationToken));

    /// <summary>
    /// How this caregiver's own work is going. Part of a paid plan, so a 403 here means
    /// the plan does not include it rather than that anything is wrong.
    /// </summary>
    [HttpGet("insights")]
    [ProducesResponseType(typeof(CaregiverInsightsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverInsightsResponse>> Insights(
        CancellationToken cancellationToken) =>
        Ok(await getInsights.HandleAsync(new GetInsightsQuery(), cancellationToken));

    [HttpPut]
    [ProducesResponseType(typeof(CaregiverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CaregiverProfileResponse>> Upsert(
        [FromBody] UpsertCaregiverProfileCommand command,
        [FromServices] IValidator<UpsertCaregiverProfileCommand> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await upsert.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Opens or closes this caregiver to new requests — the last condition on being
    /// listed. Turning it on requires verification; turning it off never does.
    /// </summary>
    [HttpPut("accepting-work")]
    [ProducesResponseType(typeof(CaregiverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverProfileResponse>> SetAcceptingWork(
        [FromBody] SetAcceptingWorkRequest request,
        CancellationToken cancellationToken) =>
        Ok(await setAcceptingWork.HandleAsync(
            new SetAcceptingWorkCommand(request.AcceptingWork), cancellationToken));

    /// <summary>
    /// Opts this caregiver into (or out of) the modest search boost for someone open to
    /// a booking on short notice. See <c>CaregiverRanking</c> for exactly how small that
    /// boost is, and <c>CaregiverProfile.AvailableForLastMinuteRequests</c> for why it can
    /// never promise a specific caregiver is free right now.
    /// </summary>
    [HttpPut("available-now")]
    [ProducesResponseType(typeof(CaregiverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverProfileResponse>> SetAvailableForLastMinuteRequests(
        [FromBody] SetAvailableForLastMinuteRequestsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await setAvailableForLastMinuteRequests.HandleAsync(
            new SetAvailableForLastMinuteRequestsCommand(request.AvailableForLastMinuteRequests),
            cancellationToken));

    [HttpPut("availability")]
    [ProducesResponseType(typeof(CaregiverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverProfileResponse>> SetAvailability(
        [FromBody] SetAvailabilityRequest request,
        [FromServices] IValidator<SetAvailabilityCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SetAvailabilityCommand(request.Windows ?? []);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await setAvailability.HandleAsync(command, cancellationToken));
    }

    [HttpPut("services")]
    [ProducesResponseType(typeof(CaregiverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverProfileResponse>> SetServices(
        [FromBody] SetServicesRequest request,
        [FromServices] IValidator<SetServicesCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SetServicesCommand(request.ServiceCodes ?? []);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await setServices.HandleAsync(command, cancellationToken));
    }

    [HttpPost("certifications")]
    [ProducesResponseType(typeof(CaregiverCertificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverCertificationResponse>> SubmitCertification(
        [FromBody] SubmitCertificationCommand command,
        [FromServices] IValidator<SubmitCertificationCommand> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await submitCertification.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Uploads the certificate itself.
    ///
    /// Multipart rather than a JSON body with base64: a phone photograph is several
    /// megabytes, and base64 inflates it by a third before it reaches a request-size limit.
    ///
    /// The request-size cap is set here as well as in the handler. This one refuses the
    /// body before it is buffered; the handler's refuses it with a message a person can
    /// read. Only the second is user-facing, and only the first protects the server.
    /// </summary>
    [HttpPost("certifications/{caregiverCertificationId:guid}/document")]
    [RequestSizeLimit(UploadCertificationDocumentHandler.MaximumBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadCertificationDocument(
        Guid caregiverCertificationId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest();
        }

        await using var content = file.OpenReadStream();

        await uploadDocument.HandleAsync(
            new UploadCertificationDocumentCommand(
                caregiverCertificationId,
                content,
                file.FileName,
                file.ContentType,
                file.Length),
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Electronically signs the Caregiver Service Agreement — always the version in
    /// force today, never one the caller names. A caregiver cannot be listed without
    /// this; see <see cref="Domain.Caregivers.CaregiverProfile.IsListable"/>.
    /// </summary>
    [HttpPost("service-agreement/accept")]
    [ProducesResponseType(typeof(CaregiverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverProfileResponse>> AcceptServiceAgreement(
        [FromBody] AcceptServiceAgreementRequest request,
        [FromServices] IValidator<AcceptServiceAgreementCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new AcceptServiceAgreementCommand(request.SignedName ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await acceptServiceAgreement.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Records an insurance policy the caregiver claims to hold. Stored as Pending until
    /// staff verify it — see docs/TRUST_AND_SAFETY.md. GoSit verifies coverage; it never
    /// issues or holds it.
    /// </summary>
    [HttpPost("insurance")]
    [ProducesResponseType(typeof(CaregiverInsurancePolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverInsurancePolicyResponse>> SubmitInsurance(
        [FromBody] SubmitInsuranceRequest request,
        [FromServices] IValidator<SubmitInsuranceCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new SubmitInsuranceCommand(
            request.CoverageType,
            request.Insurer ?? string.Empty,
            request.PolicyReference ?? string.Empty,
            request.LiabilityLimit,
            request.EffectiveOn,
            request.ExpiresOn);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await submitInsurance.HandleAsync(command, cancellationToken));
    }

    /// <summary>Uploads proof of insurance. Mirrors the certification document upload.</summary>
    [HttpPost("insurance/{caregiverInsurancePolicyId:guid}/document")]
    [RequestSizeLimit(UploadInsuranceDocumentHandler.MaximumBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadInsuranceDocument(
        Guid caregiverInsurancePolicyId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest();
        }

        await using var content = file.OpenReadStream();

        await uploadInsuranceDocument.HandleAsync(
            new UploadInsuranceDocumentCommand(
                caregiverInsurancePolicyId, content, file.FileName, file.ContentType, file.Length),
            cancellationToken);

        return NoContent();
    }
}

public sealed record SetAcceptingWorkRequest(bool AcceptingWork);

public sealed record SetAvailableForLastMinuteRequestsRequest(bool AvailableForLastMinuteRequests);

public sealed record SetAvailabilityRequest(IReadOnlyList<AvailabilityWindowDto>? Windows);

public sealed record SetServicesRequest(IReadOnlyList<string>? ServiceCodes);

public sealed record AcceptServiceAgreementRequest(string? SignedName);

public sealed record SubmitInsuranceRequest(
    InsuranceCoverageType CoverageType,
    string? Insurer,
    string? PolicyReference,
    decimal LiabilityLimit,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn);
