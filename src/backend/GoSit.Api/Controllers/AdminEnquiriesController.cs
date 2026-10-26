using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Enquiries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Messages sent through the public contact form, for staff. The sender's name, address
/// and message are personal information: staff only, and every closing action is audited.
/// </summary>
[ApiController]
[Route("api/v1/admin/enquiries")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminEnquiriesController(
    IQueryHandler<ListEnquiriesQuery, IReadOnlyList<AdminEnquiry>> list,
    ICommandHandler<CloseEnquiryCommand, Unit> close) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminEnquiry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminEnquiry>>> List(
        [FromQuery] bool includeClosed = false,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await list.HandleAsync(new ListEnquiriesQuery(includeClosed, take), cancellationToken));

    [HttpPost("{enquiryId:guid}/handled")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Handled(Guid enquiryId, CancellationToken cancellationToken)
    {
        await close.HandleAsync(new CloseEnquiryCommand(enquiryId, IsSpam: false), cancellationToken);
        return NoContent();
    }

    [HttpPost("{enquiryId:guid}/spam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Spam(Guid enquiryId, CancellationToken cancellationToken)
    {
        await close.HandleAsync(new CloseEnquiryCommand(enquiryId, IsSpam: true), cancellationToken);
        return NoContent();
    }
}
