using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.AddCareRecipient;
using GoSit.Application.Features.CareRecipients.GetCareRecipient;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.CareRecipients.UpdateCareNeeds;
using GoSit.Application.Features.CareRequesters;
using GoSit.Domain.CareRecipients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Care recipients belonging to the authenticated care requester.
///
/// The route carries a recipient id, so every handler re-checks ownership against the
/// caller's own profile. A recipient owned by someone else returns 404, never 403 —
/// a 403 would confirm the id exists.
/// </summary>
[ApiController]
[Route("api/v1/care-requesters/me/care-recipients")]
[Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
public sealed class CareRecipientsController(
    ICommandHandler<AddCareRecipientCommand, CareRecipientResponse> add,
    ICommandHandler<UpdateCareNeedsCommand, CareRecipientResponse> updateCareNeeds,
    ICommandHandler<RemoveCareRecipientCommand, Unit> remove,
    IQueryHandler<GetCareRecipientQuery, CareRecipientResponse> get) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CareRecipientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CareRecipientResponse>> Add(
        [FromBody] AddCareRecipientRequest request,
        [FromServices] IValidator<AddCareRecipientCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new AddCareRecipientCommand(
            request.FirstName, request.LastName, request.DateOfBirth, request.RecipientType);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var created = await add.HandleAsync(command, cancellationToken);

        return CreatedAtAction(nameof(Get), new { careRecipientId = created.Id }, created);
    }

    [HttpGet("{careRecipientId:guid}")]
    [ProducesResponseType(typeof(CareRecipientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CareRecipientResponse>> Get(
        Guid careRecipientId,
        CancellationToken cancellationToken) =>
        Ok(await get.HandleAsync(new GetCareRecipientQuery(careRecipientId), cancellationToken));

    /// <summary>Replaces the recorded care needs. Sensitive: see UpdateCareNeedsCommand.</summary>
    [HttpPut("{careRecipientId:guid}/care-needs")]
    [ProducesResponseType(typeof(CareRecipientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CareRecipientResponse>> UpdateCareNeeds(
        Guid careRecipientId,
        [FromBody] UpdateCareNeedsRequest request,
        [FromServices] IValidator<UpdateCareNeedsCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCareNeedsCommand(
            careRecipientId,
            request.MobilityNeeds,
            request.CommunicationStyle,
            request.SensoryNeeds,
            request.BehaviourSupportNotes,
            request.DietaryRestrictions,
            request.Allergies,
            request.ToiletingAssistance,
            request.FeedingAssistance,
            request.RoutinePreferences,
            request.Triggers,
            request.CalmingStrategies,
            request.EmergencyInstructions,
            request.RequiredSkills ?? []);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await updateCareNeeds.HandleAsync(command, cancellationToken));
    }

    [HttpDelete("{careRecipientId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid careRecipientId, CancellationToken cancellationToken)
    {
        await remove.HandleAsync(new RemoveCareRecipientCommand(careRecipientId), cancellationToken);
        return NoContent();
    }
}

public sealed record AddCareRecipientRequest(
    string FirstName,
    string? LastName,
    DateOnly DateOfBirth,
    CareRecipientType RecipientType);

public sealed record UpdateCareNeedsRequest(
    string? MobilityNeeds,
    string? CommunicationStyle,
    string? SensoryNeeds,
    string? BehaviourSupportNotes,
    string? DietaryRestrictions,
    string? Allergies,
    bool ToiletingAssistance,
    bool FeedingAssistance,
    string? RoutinePreferences,
    string? Triggers,
    string? CalmingStrategies,
    string? EmergencyInstructions,
    IReadOnlyList<string>? RequiredSkills);
