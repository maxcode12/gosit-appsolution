using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Areas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GoSit.Api.Controllers;

/// <summary>
/// Public location lookups for the website's "caregivers near you" box.
///
/// Anonymous because it carries no personal information: a postal area and a count. It is
/// rate-limited per IP, open to the company's own website only, and never returns a
/// caregiver, a name, or a street.
/// </summary>
[ApiController]
[Route("api/v1/areas")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicLookup)]
[EnableCors(CorsPolicies.PublicReads)]
public sealed class AreasController(
    IQueryHandler<GetAreaOverviewQuery, AreaOverviewResponse> overview,
    IQueryHandler<SuggestAreasQuery, AreaSuggestionsResponse> suggest) : ControllerBase
{
    /// <summary>The number of caregivers near a postal code or place.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AreaOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AreaOverviewResponse>> Overview(
        [FromQuery] string? query,
        [FromQuery] int? radiusKm,
        [FromServices] IValidator<GetAreaOverviewQuery> validator,
        CancellationToken cancellationToken)
    {
        var request = new GetAreaOverviewQuery(
            query ?? string.Empty,
            radiusKm ?? GetAreaOverviewQuery.DefaultRadiusKm);

        await validator.ValidateOrThrowAsync(request, cancellationToken);

        return Ok(await overview.HandleAsync(request, cancellationToken));
    }

    /// <summary>Postal areas and places matching what has been typed so far.</summary>
    [HttpGet("suggest")]
    [ProducesResponseType(typeof(AreaSuggestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AreaSuggestionsResponse>> Suggest(
        [FromQuery] string? q,
        [FromServices] IValidator<SuggestAreasQuery> validator,
        CancellationToken cancellationToken)
    {
        var request = new SuggestAreasQuery(q ?? string.Empty);

        await validator.ValidateOrThrowAsync(request, cancellationToken);

        return Ok(await suggest.HandleAsync(request, cancellationToken));
    }
}
