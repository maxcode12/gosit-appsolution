using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;

namespace GoSit.Application.Features.Areas;

/// <summary>
/// "How many caregivers are near this postal code?", answerable without signing in.
///
/// It returns a count and nothing else. Who the caregivers are, what they charge and
/// where exactly they are stay behind sign-in: a public list would tell anyone who asks
/// who is available for care work and in which neighbourhood. A number lets a family
/// decide whether GoSit is worth joining, and tells us how to respond when the answer
/// is none.
/// </summary>
public sealed record GetAreaOverviewQuery(string Query, int RadiusKm = GetAreaOverviewQuery.DefaultRadiusKm)
    : IQuery<AreaOverviewResponse>
{
    public const int DefaultRadiusKm = 25;
}

public sealed record AreaSummary(string Code, string Name, string Province);

public sealed record CategoryCount(string Code, string Name, int Count);

public sealed record AreaOverviewResponse(
    AreaSummary Area,
    int RadiusKm,
    int CaregiverCount,
    IReadOnlyList<CategoryCount> Categories);

public sealed class GetAreaOverviewValidator : AbstractValidator<GetAreaOverviewQuery>
{
    public GetAreaOverviewValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(40);
        RuleFor(x => x.RadiusKm).InclusiveBetween(1, 100);
    }
}

public sealed class GetAreaOverviewHandler(
    IPostalAreaDirectory areas,
    ICaregiverSearchRepository search)
    : IQueryHandler<GetAreaOverviewQuery, AreaOverviewResponse>
{
    public async Task<AreaOverviewResponse> HandleAsync(
        GetAreaOverviewQuery query,
        CancellationToken cancellationToken)
    {
        // A postal code first. Failing that, the first place that matches the text, so
        // "Hamilton" works as well as "L8P".
        var area = areas.Find(query.Query) ?? areas.Suggest(query.Query, 1).FirstOrDefault()
            ?? throw new NotFoundException("We could not find that postal code or place in Canada.");

        // "Near" is measured between postal-area centres, which is the precision the data
        // has and the precision it is honest to claim: the area itself, then every area whose
        // centre lies within the radius.
        var inRange = areas.Within(area.Latitude, area.Longitude, query.RadiusKm)
            .Select(a => a.Code)
            .ToList();

        var nearby = await search.ListListableInAreasAsync(inRange, cancellationToken);

        var categories = (await search.ListCategoriesAsync(cancellationToken))
            .Select(category => new CategoryCount(
                category.Code,
                category.Name,
                nearby.Count(c => c.CategoryCodes.Contains(category.Code))))
            .ToList();

        return new AreaOverviewResponse(
            new AreaSummary(area.Code, area.Name, area.Province),
            query.RadiusKm,
            nearby.Count,
            categories);
    }
}

/// <summary>Postal areas and places matching typed text, for a search box.</summary>
public sealed record SuggestAreasQuery(string Text) : IQuery<AreaSuggestionsResponse>;

public sealed record AreaSuggestionsResponse(IReadOnlyList<AreaSummary> Areas);

public sealed class SuggestAreasValidator : AbstractValidator<SuggestAreasQuery>
{
    public SuggestAreasValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MinimumLength(2).MaximumLength(40);
    }
}

public sealed class SuggestAreasHandler(IPostalAreaDirectory areas)
    : IQueryHandler<SuggestAreasQuery, AreaSuggestionsResponse>
{
    private const int MaxSuggestions = 8;

    public Task<AreaSuggestionsResponse> HandleAsync(SuggestAreasQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new AreaSuggestionsResponse(
            areas.Suggest(query.Text, MaxSuggestions)
                .Select(a => new AreaSummary(a.Code, a.Name, a.Province))
                .ToList()));
}
