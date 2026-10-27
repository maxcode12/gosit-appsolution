using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Abstractions;

/// <summary>Filters applied in the database before ranking happens in memory.</summary>
public sealed record CaregiverSearchFilter(
    Guid? CareServiceId,
    string? City,
    // Forward sortation area — the first three characters of a postal code.
    string? PostalCodePrefix,
    decimal? MaxHourlyRate,
    int? MinYearsOfExperience,
    decimal? MinRating,
    DayOfWeek? Day);

public interface ICaregiverSearchRepository
{
    /// <summary>
    /// Returns listable caregivers matching the coarse filters, with the collections
    /// ranking needs already loaded.
    ///
    /// Bounded by <paramref name="limit"/>: eligibility, distance, and ordering are
    /// computed in memory, so the candidate set has to stay small. This is right for
    /// launch scale and is the first thing to replace with a search index when the
    /// marketplace grows — see docs/ARCHITECTURE.md.
    /// </summary>
    Task<IReadOnlyList<CaregiverProfile>> FindCandidatesAsync(
        CaregiverSearchFilter filter,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Listable caregivers whose postal area is one of those given, for counting.
    ///
    /// Returns only the area and category codes, never a profile: the public "caregivers
    /// near you" count must not be one projection away from exposing a person.
    /// </summary>
    Task<IReadOnlyList<NearbyCaregiver>> ListListableInAreasAsync(
        IReadOnlyCollection<string> areaCodes,
        CancellationToken cancellationToken);

    /// <summary>A single caregiver for the public detail page, or null if not listable.</summary>
    Task<CaregiverProfile?> FindListableByIdAsync(Guid caregiverProfileId, CancellationToken cancellationToken);

    Task<CareService?> FindServiceByCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<CareServiceCategory>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CareService>> ListServicesAsync(CancellationToken cancellationToken);

    /// <summary>The caregiver ids this requester has saved, for flagging results.</summary>
    Task<IReadOnlySet<Guid>> ListFavoriteCaregiverIdsAsync(
        Guid careRequesterProfileId,
        CancellationToken cancellationToken);

    /// <summary>Names for the caregivers in a result set, keyed by user id.</summary>
    Task<IReadOnlyDictionary<Guid, CaregiverName>> ListCaregiverNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

/// <summary>A listable caregiver reduced to their postal area and the categories they serve.</summary>
public sealed record NearbyCaregiver(string AreaCode, IReadOnlyList<string> CategoryCodes);

/// <summary>
/// A caregiver's name for display. Only ever rendered as first name plus last initial
/// in public responses.
/// </summary>
public sealed record CaregiverName(string FirstName, string LastName);
