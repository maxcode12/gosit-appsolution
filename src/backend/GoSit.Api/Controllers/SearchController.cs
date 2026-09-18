using FluentValidation;
using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareServices.ListCareServices;
using GoSit.Application.Features.Search;
using GoSit.Application.Features.Search.GetCaregiverDetail;
using GoSit.Application.Features.Search.SearchCaregivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Caregiver discovery.
///
/// Restricted to signed-in care requesters. Caregiver listings are not public in the
/// MVP: making them anonymous would expose who is available for care work, and where,
/// to anyone who asks. Public service and location landing pages use the anonymous
/// taxonomy endpoint instead.
/// </summary>
[ApiController]
[Route("api/v1/caregivers")]
[Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
public sealed class SearchController(
    IQueryHandler<SearchCaregiversQuery, CaregiverSearchResponse> searchCaregivers,
    IQueryHandler<GetCaregiverDetailQuery, CaregiverDetailResponse> getDetail) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType(typeof(CaregiverSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverSearchResponse>> Search(
        [FromQuery] CaregiverSearchRequest request,
        [FromServices] IValidator<SearchCaregiversQuery> validator,
        CancellationToken cancellationToken)
    {
        var query = new SearchCaregiversQuery(
            request.ServiceCode,
            request.City,
            request.PostalCode,
            request.RadiusKm,
            request.Day,
            request.StartTime,
            request.EndTime,
            request.MaxHourlyRate,
            request.MinYearsOfExperience,
            request.MinRating,
            request.Certifications,
            request.Page ?? 1,
            request.PageSize ?? 20);

        await validator.ValidateOrThrowAsync(query, cancellationToken);

        return Ok(await searchCaregivers.HandleAsync(query, cancellationToken));
    }

    [HttpGet("{caregiverProfileId:guid}")]
    [ProducesResponseType(typeof(CaregiverDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaregiverDetailResponse>> Get(
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        Ok(await getDetail.HandleAsync(new GetCaregiverDetailQuery(caregiverProfileId), cancellationToken));
}

/// <summary>
/// Public reference data: the care-service taxonomy. Anonymous because it contains no
/// personal information and the marketing site renders it for SEO landing pages.
/// </summary>
[ApiController]
[Route("api/v1/care-services")]
[AllowAnonymous]
public sealed class CareServicesController(
    IQueryHandler<ListCareServicesQuery, CareServiceCatalogueResponse> listServices) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CareServiceCatalogueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CareServiceCatalogueResponse>> List(CancellationToken cancellationToken) =>
        Ok(await listServices.HandleAsync(new ListCareServicesQuery(), cancellationToken));
}

public sealed record CaregiverSearchRequest(
    string? ServiceCode,
    string? City,
    string? PostalCode,
    int? RadiusKm,
    DayOfWeek? Day,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal? MaxHourlyRate,
    int? MinYearsOfExperience,
    decimal? MinRating,
    IReadOnlyList<string>? Certifications,
    int? Page,
    int? PageSize);
