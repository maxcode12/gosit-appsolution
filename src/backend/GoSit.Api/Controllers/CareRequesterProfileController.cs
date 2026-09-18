using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRequesters;
using GoSit.Application.Features.CareRequesters.GetProfile;
using GoSit.Application.Features.CareRequesters.UpsertProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// The caller's own care-requester profile. There is deliberately no route that takes
/// a profile id: every operation resolves the profile from the access token, so one
/// requester cannot address another's profile at all.
/// </summary>
[ApiController]
[Route("api/v1/care-requesters/me")]
[Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
public sealed class CareRequesterProfileController(
    ICommandHandler<UpsertCareRequesterProfileCommand, CareRequesterProfileResponse> upsert,
    IQueryHandler<GetCareRequesterProfileQuery, CareRequesterProfileResponse> getProfile) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CareRequesterProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CareRequesterProfileResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await getProfile.HandleAsync(new GetCareRequesterProfileQuery(), cancellationToken));

    [HttpPut]
    [ProducesResponseType(typeof(CareRequesterProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CareRequesterProfileResponse>> Upsert(
        [FromBody] UpsertCareRequesterProfileCommand command,
        [FromServices] IValidator<UpsertCareRequesterProfileCommand> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(command, cancellationToken);

        return Ok(await upsert.HandleAsync(command, cancellationToken));
    }
}
