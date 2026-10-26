using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Enquiries;
using GoSit.Domain.Enquiries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GoSit.Api.Controllers;

/// <summary>
/// The public website's contact form. Anonymous by nature, so it is guarded by a rate
/// limit, a spam trap, a small body limit, and a cross-site rule that names only the
/// company's own website.
/// </summary>
[ApiController]
[Route("api/v1/enquiries")]
public sealed class EnquiriesController(ICommandHandler<SubmitEnquiryCommand, Unit> submit) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicForm)]
    [EnableCors(CorsPolicies.PublicForms)]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitEnquiryRequest request,
        [FromServices] IValidator<SubmitEnquiryCommand> validator,
        CancellationToken cancellationToken)
    {
        // A field no person can see. Anything that fills it is a script. It gets the same
        // answer as a real submission, so the script learns nothing and does not adapt.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Accepted();
        }

        var command = new SubmitEnquiryCommand(
            request.Name ?? string.Empty,
            request.Email ?? string.Empty,
            request.Kind,
            request.Message ?? string.Empty,
            request.ConsentGiven,
            "website");

        await validator.ValidateOrThrowAsync(command, cancellationToken);
        await submit.HandleAsync(command, cancellationToken);

        return Accepted();
    }
}

public sealed record SubmitEnquiryRequest(
    string? Name,
    string? Email,
    EnquiryKind Kind,
    string? Message,
    bool ConsentGiven,
    string? Website);
