using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Subscriptions;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using GoSit.Domain.Common.ValueObjects;
using GoSit.Domain.Subscriptions;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Search.SearchCaregivers;

public sealed class SearchCaregiversHandler(
    ICaregiverSearchRepository search,
    ICareRequesterProfileRepository requesterProfiles,
    ICaregiverExposureRepository exposure,
    EntitlementService entitlements,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<SearchCaregiversHandler> logger)
    : IQueryHandler<SearchCaregiversQuery, CaregiverSearchResponse>
{
    /// <summary>
    /// Ceiling on the candidate set pulled back for in-memory ranking. Generous next to
    /// a page of 20, so paging stays correct well past launch volumes, while keeping a
    /// pathological query from loading the whole table.
    /// </summary>
    private const int CandidateLimit = 500;

    public async Task<CaregiverSearchResponse> HandleAsync(
        SearchCaregiversQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var today = DateOnly.FromDateTime(clock.UtcNow);

        await EnsureAdvancedFiltersAllowedAsync(query, userId, cancellationToken);

        CareService? service = null;
        if (!string.IsNullOrWhiteSpace(query.ServiceCode))
        {
            service = await search.FindServiceByCodeAsync(query.ServiceCode, cancellationToken)
                ?? throw new NotFoundException("That care service is not recognized.");
        }

        // The requester's own profile supplies the search origin when no location was
        // typed, and tells us which caregivers they have already saved.
        var requesterProfile = await requesterProfiles.FindByUserIdAsync(userId, cancellationToken);

        // Distance is measured from the requester's saved address. A typed postal code
        // narrows results by forward sortation area instead, because an address only
        // gains coordinates once a geocoding provider is configured.
        var origin = requesterProfile?.Address;

        var filter = new CaregiverSearchFilter(
            service?.Id,
            NormalizeCity(query.City),
            ToPostalCodePrefix(query.PostalCode),
            query.MaxHourlyRate,
            query.MinYearsOfExperience,
            query.MinRating,
            query.Day);

        var candidates = await search.FindCandidatesAsync(filter, CandidateLimit, cancellationToken);

        if (candidates.Count == CandidateLimit)
        {
            // Silent truncation would look like "these are all the matches".
            logger.LogWarning(
                "Caregiver search hit the candidate ceiling of {Limit}; results may be incomplete",
                CandidateLimit);
        }

        var eligible = candidates
            .Where(c => service is null || c.IsEligibleFor(service, today))
            .Where(c => MeetsRequestedCertifications(c, query.RequiredCertificationCodes, today))
            .Where(c => WithinRadius(c, origin, query.RadiusKm))
            .ToList();

        // Resolved for the whole eligible set in one query, because ranking needs the
        // answer for every candidate before it can sort any of them.
        var promoted = await entitlements.WhoHasAsync(
            eligible.Select(c => c.UserId).Distinct().ToList(),
            SubscriptionFeature.ProfilePromotion,
            cancellationToken);

        var ranked = CaregiverRanking
            .Apply(eligible.Select(c => new RankedCaregiver(
                c,
                MatchesAvailability: MatchesRequestedWindow(c, query),
                DistanceKm: CaregiverRanking.DistanceFrom(origin, c),
                IsPromoted: promoted.Contains(c.UserId))))
            .ToList();

        var page = ranked
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var favorites = requesterProfile is null
            ? new HashSet<Guid>()
            : (await search.ListFavoriteCaregiverIdsAsync(requesterProfile.Id, cancellationToken))
                .ToHashSet();

        var names = await search.ListCaregiverNamesAsync(
            page.Select(c => c.Profile.UserId).ToList(), cancellationToken);

        var results = page
            .Select(c => ToResult(c, names, favorites, today))
            .ToList();

        await RecordImpressionsAsync(page, today, cancellationToken);

        // Distance ranking is only meaningful once addresses are geocoded, and which
        // side is missing decides what there is to say about it.
        var reason = DistanceUnavailableReasonFor(origin, ranked);

        return new CaregiverSearchResponse(
            results, ranked.Count, query.Page, query.PageSize, reason is not null, reason);
    }

    /// <summary>
    /// Counts the caregivers on this page as having been seen.
    ///
    /// Only the page, not the ranked set: a caregiver on page four was not shown to
    /// anyone, and counting them would inflate the one number a caregiver is most likely
    /// to read as "how visible am I".
    ///
    /// Nothing about who was searching is recorded — see
    /// <see cref="Domain.Caregivers.CaregiverExposure"/> for why that is the shape rather
    /// than an omission.
    ///
    /// A failure here is swallowed. This is a counter on the hottest path in the product,
    /// and refusing to return caregivers to a family because a statistics row would not
    /// write is the wrong trade by a wide margin.
    /// </summary>
    private async Task RecordImpressionsAsync(
        IReadOnlyList<RankedCaregiver> page,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (page.Count == 0)
        {
            return;
        }

        try
        {
            await exposure.RecordImpressionsAsync(
                page.Select(c => c.Profile.Id).ToList(), today, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Search impressions were not recorded for this page");
        }
    }

    private static CaregiverSearchResult ToResult(
        RankedCaregiver candidate,
        IReadOnlyDictionary<Guid, CaregiverName> names,
        IReadOnlySet<Guid> favorites,
        DateOnly today)
    {
        var profile = candidate.Profile;
        var name = names.TryGetValue(profile.UserId, out var found) ? found : new CaregiverName("Caregiver", string.Empty);

        return new CaregiverSearchResult(
            profile.Id,
            name.FirstName,
            // Last initial only. A search result never carries a full legal name.
            name.LastName.Length > 0 ? name.LastName[..1] : string.Empty,
            profile.Bio,
            profile.YearsOfExperience,
            profile.HourlyRate,
            profile.NightlyRate,
            // City and forward sortation area, never the street address.
            profile.Address?.ToApproximate() ?? string.Empty,
            candidate.DistanceKm is { } km ? Math.Round(km, 1) : null,
            profile.AverageRating,
            profile.ReviewCount,
            profile.VerificationStatus == VerificationStatus.Verified,
            profile.Services.Select(s => s.Code).OrderBy(c => c).ToList(),
            // Only currently-valid certifications, so an expired one is never advertised.
            profile.ValidCertificationCodesOn(today).OrderBy(c => c).ToList(),
            favorites.Contains(profile.Id));
    }

    /// <summary>
    /// Why proximity ranking was not applied, or null when it was.
    ///
    /// Only one of these is the reader's to fix, and telling someone with an address on
    /// file to go and add one sends them looking for a setting they already filled in.
    /// </summary>
    private static string? DistanceUnavailableReasonFor(
        Address? origin,
        IReadOnlyList<RankedCaregiver> ranked)
    {
        if (origin is null)
        {
            return DistanceUnavailableReasons.NoAddress;
        }

        if (origin.Latitude is null)
        {
            return DistanceUnavailableReasons.AddressNotLocated;
        }

        // Nobody to measure against is not the same as nothing to measure from. With no
        // results at all there is nothing to explain either.
        return ranked.Count > 0 && ranked.All(c => c.DistanceKm is null)
            ? DistanceUnavailableReasons.CaregiversNotLocated
            : null;
    }

    /// <summary>
    /// Refuses the filters sold as <see cref="SubscriptionFeature.AdvancedSearchFilters"/>.
    ///
    /// Refused rather than quietly dropped. Returning a full result set to someone who
    /// asked for "only caregivers with First Aid" would present unfiltered results as
    /// filtered ones — in a care marketplace, that is the kind of silence that gets
    /// someone booked who should not have been. A refusal that names the reason lets the
    /// UI say so.
    ///
    /// Location, service, availability and price are not gated: they are how anyone
    /// finds care at all. Nor is rating — see <see cref="EntitlementPolicy"/>.
    /// </summary>
    private async Task EnsureAdvancedFiltersAllowedAsync(
        SearchCaregiversQuery query,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var usesAdvanced =
            query.RequiredCertificationCodes is { Count: > 0 }
            || query.MinYearsOfExperience.HasValue;

        if (!usesAdvanced)
        {
            return;
        }

        if (!await entitlements.HasAsync(
                userId, SubscriptionFeature.AdvancedSearchFilters, cancellationToken))
        {
            throw new ForbiddenException(EntitlementPolicy.AdvancedFilterDescription);
        }
    }

    private static bool MeetsRequestedCertifications(
        CaregiverProfile caregiver,
        IReadOnlyList<string>? requiredCodes,
        DateOnly today) =>
        requiredCodes is null or { Count: 0 } || caregiver.MeetsRequirements(requiredCodes, today);

    private static bool MatchesRequestedWindow(CaregiverProfile caregiver, SearchCaregiversQuery query)
    {
        if (query.Day is not { } day || query.StartTime is not { } start || query.EndTime is not { } end)
        {
            return true;
        }

        return AvailabilityMatching.Covers(caregiver.Availability, day, start, end);
    }

    /// <summary>
    /// Applies the radius filter only where both ends are geocoded. Excluding
    /// caregivers whose distance is unknown would empty the results entirely while no
    /// geocoding provider is configured.
    /// </summary>
    private static bool WithinRadius(CaregiverProfile caregiver, Address? origin, int? radiusKm)
    {
        if (radiusKm is not { } radius || origin is null)
        {
            return true;
        }

        var distance = GeoDistance.BetweenKm(origin, caregiver.Address);
        return distance is null || distance <= radius;
    }

    /// <summary>
    /// Reduces a postal code to its forward sortation area, which is the useful unit of
    /// proximity without coordinates: "M5S 1M2" and "M5S 2N4" are the same neighbourhood.
    /// </summary>
    private static string? ToPostalCodePrefix(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        var normalized = Address.NormalizePostalCode(postalCode);
        return normalized.Length >= 3 ? normalized[..3] : null;
    }

    private static string? NormalizeCity(string? city) =>
        string.IsNullOrWhiteSpace(city) ? null : city.Trim();
}
