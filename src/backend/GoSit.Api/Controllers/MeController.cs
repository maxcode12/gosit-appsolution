using GoSit.Application.Abstractions;
using GoSit.Application.Features.Authentication.GetCurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeController(
    IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> getCurrentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await getCurrentUser.HandleAsync(new GetCurrentUserQuery(), cancellationToken));
}
