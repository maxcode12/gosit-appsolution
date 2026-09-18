using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Pets;
using GoSit.Domain.Pets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// A family's pets. Read back as part of <c>GET /care-requesters/me</c>; a caregiver sees a
/// pet only through a booking, and its care notes only once they have accepted.
/// </summary>
[ApiController]
[Route("api/v1/care-requesters/me/pets")]
[Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
public sealed class PetsController(
    ICommandHandler<AddPetCommand, PetResponse> add,
    ICommandHandler<UpdatePetCommand, PetResponse> update,
    ICommandHandler<RemovePetCommand, Unit> remove) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PetResponse>> Add(
        [FromBody] SavePetRequest request,
        [FromServices] IValidator<AddPetCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new AddPetCommand(
            request.Name ?? string.Empty,
            request.Species,
            request.Breed,
            request.Size,
            request.FeedingInstructions,
            request.Medication,
            request.Behaviour,
            request.VetClinic,
            request.OtherNotes);

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        var created = await add.HandleAsync(command, cancellationToken);

        return Created($"/api/v1/care-requesters/me/pets/{created.Id}", created);
    }

    [HttpPut("{petId:guid}")]
    [ProducesResponseType(typeof(PetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PetResponse>> Update(
        Guid petId,
        [FromBody] SavePetRequest request,
        [FromServices] IValidator<UpdatePetCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePetCommand(
            petId,
            request.Name ?? string.Empty,
            request.Species,
            request.Breed,
            request.Size,
            request.FeedingInstructions,
            request.Medication,
            request.Behaviour,
            request.VetClinic,
            request.OtherNotes);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await update.HandleAsync(command, cancellationToken));
    }

    [HttpDelete("{petId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid petId, CancellationToken cancellationToken)
    {
        await remove.HandleAsync(new RemovePetCommand(petId), cancellationToken);
        return NoContent();
    }
}

public sealed record SavePetRequest(
    string? Name,
    PetSpecies Species,
    string? Breed,
    PetSize? Size,
    string? FeedingInstructions,
    string? Medication,
    string? Behaviour,
    string? VetClinic,
    string? OtherNotes);
