using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.CareRequesters;
using GoSit.Application.Features.CareRequesters.EmergencyContacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Emergency contacts belonging to the authenticated care requester.
///
/// Like the profile itself, there is no route that takes a profile id: the family is
/// resolved from the access token. Reading them back is part of `GET /care-requesters/me`,
/// which already carries the array — it was simply always empty, because until this
/// controller existed nothing could put anything in it.
///
/// A caregiver reads them through the booking, never here. See
/// `GET /api/v1/bookings/{id}/emergency-contacts`.
/// </summary>
[ApiController]
[Route("api/v1/care-requesters/me/emergency-contacts")]
[Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
public sealed class EmergencyContactsController(
    ICommandHandler<AddEmergencyContactCommand, EmergencyContactResponse> add,
    ICommandHandler<RemoveEmergencyContactCommand, Unit> remove) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(EmergencyContactResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmergencyContactResponse>> Add(
        [FromBody] AddEmergencyContactRequest request,
        [FromServices] IValidator<AddEmergencyContactCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new AddEmergencyContactCommand(
            request.FullName ?? string.Empty,
            request.Relationship ?? string.Empty,
            request.PhoneNumber ?? string.Empty);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        var created = await add.HandleAsync(command, cancellationToken);

        // No GET for a single contact: they are read as a set, from the profile or from a
        // booking. Created is still the honest status, so the location is the collection.
        return Created($"/api/v1/care-requesters/me/emergency-contacts/{created.Id}", created);
    }

    [HttpDelete("{emergencyContactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        Guid emergencyContactId,
        CancellationToken cancellationToken)
    {
        await remove.HandleAsync(
            new RemoveEmergencyContactCommand(emergencyContactId), cancellationToken);

        return NoContent();
    }
}

public sealed record AddEmergencyContactRequest(
    string? FullName,
    string? Relationship,
    string? PhoneNumber);
