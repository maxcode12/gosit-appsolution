using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Administration.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

public sealed record InviteStaffRequest(string Email, string FirstName, string LastName);

/// <summary>What the signed-in staff member may do, so the staff site shows only what works.</summary>
public sealed record StaffMeResponse(bool IsSuperAdmin);

/// <summary>
/// Staff accounts. Any staff member can ask what they are; only a super administrator can
/// list staff or add them. A staff account is created with a password nobody knows and the
/// person is emailed a link to choose their own.
/// </summary>
[ApiController]
[Route("api/v1/admin/staff")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminStaffController(
    IQueryHandler<ListStaffQuery, IReadOnlyList<StaffMember>> list,
    ICommandHandler<InviteStaffCommand, StaffMember> invite) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(StaffMeResponse), StatusCodes.Status200OK)]
    public ActionResult<StaffMeResponse> Me() =>
        Ok(new StaffMeResponse(User.HasClaim(
            GoSit.Infrastructure.Security.JwtTokenService.SuperAdminClaim, "true")));

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<StaffMember>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<StaffMember>>> List(CancellationToken cancellationToken) =>
        Ok(await list.HandleAsync(new ListStaffQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(StaffMember), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StaffMember>> Invite(
        [FromBody] InviteStaffRequest request,
        [FromServices] IValidator<InviteStaffCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new InviteStaffCommand(request.Email, request.FirstName, request.LastName);

        await validator.ValidateOrThrowAsync(command, cancellationToken);

        var staff = await invite.HandleAsync(command, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, staff);
    }
}
