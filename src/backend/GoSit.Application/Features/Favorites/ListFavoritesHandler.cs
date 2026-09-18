using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Search;
using GoSit.Domain.Caregivers;

namespace GoSit.Application.Features.Favorites;

public sealed class ListFavoritesHandler(
    ICareRequesterProfileRepository profiles,
    ICaregiverSearchRepository search,
    IFavoriteRepository favorites,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQueryHandler<ListFavoritesQuery, IReadOnlyList<CaregiverSearchResult>>
{
    public async Task<IReadOnlyList<CaregiverSearchResult>> HandleAsync(
        ListFavoritesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken);

        // No profile means no favourites. An empty list is the honest answer here — the
        // requester has not saved anyone, which is not an error.
        if (profile is null)
        {
            return [];
        }

        var saved = await favorites.ListSavedCaregiversAsync(profile.Id, cancellationToken);

        if (saved.Count == 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var names = await search.ListCaregiverNamesAsync(
            saved.Select(c => c.UserId).ToList(), cancellationToken);

        return saved
            .Select(caregiver => ToResult(caregiver, names, profile.Address, today))
            .ToList();
    }

    private static CaregiverSearchResult ToResult(
        CaregiverProfile caregiver,
        IReadOnlyDictionary<Guid, CaregiverName> names,
        Domain.Common.ValueObjects.Address? origin,
        DateOnly today)
    {
        var name = names.TryGetValue(caregiver.UserId, out var found) ? found : new CaregiverName("Caregiver", string.Empty);
        var distance = Domain.Common.ValueObjects.GeoDistance.BetweenKm(origin, caregiver.Address);

        return new CaregiverSearchResult(
            caregiver.Id,
            name.FirstName,
            name.LastName.Length > 0 ? name.LastName[..1] : string.Empty,
            caregiver.Bio,
            caregiver.YearsOfExperience,
            caregiver.HourlyRate,
            caregiver.NightlyRate,
            caregiver.Address?.ToApproximate() ?? string.Empty,
            distance is { } km ? Math.Round(km, 1) : null,
            caregiver.AverageRating,
            caregiver.ReviewCount,
            caregiver.VerificationStatus == VerificationStatus.Verified,
            caregiver.Services.Select(s => s.Code).OrderBy(c => c).ToList(),
            caregiver.ValidCertificationCodesOn(today).OrderBy(c => c).ToList(),
            // Everything on this list is, by definition, a favourite.
            IsFavorite: true);
    }
}
