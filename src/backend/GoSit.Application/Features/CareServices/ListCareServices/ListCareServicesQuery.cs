using GoSit.Application.Abstractions;

namespace GoSit.Application.Features.CareServices.ListCareServices;

/// <summary>
/// The care-service taxonomy. Public reference data with no personal information, so
/// it is available anonymously — the marketing site needs it for service and location
/// landing pages.
/// </summary>
public sealed record ListCareServicesQuery : IQuery<CareServiceCatalogueResponse>;

public sealed record CareServiceCatalogueResponse(IReadOnlyList<CareServiceCategoryResponse> Categories);

public sealed record CareServiceCategoryResponse(
    string Code,
    string Name,
    int DisplayOrder,
    IReadOnlyList<CareServiceResponse> Services);

public sealed record CareServiceResponse(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<string> RequiredCertificationCodes,
    IReadOnlyList<string> ApplicableRecipientTypes,
    IReadOnlyList<string> ApplicablePetSpecies,
    bool IsPetCare,
    string PricingUnit,
    string CareLocation);

public sealed class ListCareServicesHandler(ICaregiverSearchRepository search)
    : IQueryHandler<ListCareServicesQuery, CareServiceCatalogueResponse>
{
    public async Task<CareServiceCatalogueResponse> HandleAsync(
        ListCareServicesQuery query,
        CancellationToken cancellationToken)
    {
        var categories = await search.ListCategoriesAsync(cancellationToken);
        var services = await search.ListServicesAsync(cancellationToken);

        var byCategory = services
            .GroupBy(s => s.CareServiceCategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var response = categories
            .OrderBy(c => c.DisplayOrder)
            .Select(category => new CareServiceCategoryResponse(
                category.Code,
                category.Name,
                category.DisplayOrder,
                byCategory.TryGetValue(category.Id, out var categoryServices)
                    ? categoryServices
                        .OrderBy(s => s.Name)
                        .Select(s => new CareServiceResponse(
                            s.Code,
                            s.Name,
                            s.Description,
                            s.RequiredCertificationCodes,
                            s.ApplicableRecipientTypes.Select(t => t.ToString()).ToList(),
                            s.ApplicablePetSpecies.Select(t => t.ToString()).ToList(),
                            s.IsPetCare,
                            s.PricingUnit.ToString(),
                            s.CareLocation.ToString()))
                        .ToList()
                    : []))
            .ToList();

        return new CareServiceCatalogueResponse(response);
    }
}
