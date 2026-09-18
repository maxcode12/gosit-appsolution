using GoSit.Application.Abstractions;
using GoSit.Domain.CareRequesters;
using GoSit.Domain.CareServices;
using GoSit.Domain.Caregivers;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class CaregiverSearchRepository(GoSitDbContext context) : ICaregiverSearchRepository
{
    /// <summary>
    /// Caregivers eligible to appear in search at all: verified, accepting work, and
    /// with a location on file. Applied before every other filter so an unverified
    /// caregiver can never reach a result set by any route.
    /// </summary>
    private IQueryable<CaregiverProfile> ListableCaregivers =>
        context.CaregiverProfiles
            .AsNoTracking()
            .Where(p => p.VerificationStatus == VerificationStatus.Verified)
            .Where(p => p.AcceptingWork)
            .Where(p => p.Address != null);

    public async Task<IReadOnlyList<CaregiverProfile>> FindCandidatesAsync(
        CaregiverSearchFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = ListableCaregivers;

        if (filter.CareServiceId is { } serviceId)
        {
            query = query.Where(p => p.Services.Any(s => s.Id == serviceId));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            query = query.Where(p => p.Address!.City.ToLower() == filter.City.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(filter.PostalCodePrefix))
        {
            query = query.Where(p => p.Address!.PostalCode.StartsWith(filter.PostalCodePrefix));
        }

        if (filter.MaxHourlyRate is { } maxRate)
        {
            query = query.Where(p => p.HourlyRate <= maxRate);
        }

        if (filter.MinYearsOfExperience is { } minExperience)
        {
            query = query.Where(p => p.YearsOfExperience >= minExperience);
        }

        if (filter.MinRating is { } minRating)
        {
            // An unrated caregiver does not meet a minimum-rating filter: with no reviews
            // there is no evidence they clear the bar.
            query = query.Where(p => p.AverageRating != null && p.AverageRating >= minRating);
        }

        if (filter.Day is { } day)
        {
            // Coarse filter only — whether a slot covers the requested times is decided
            // in memory, where the interval logic lives.
            query = query.Where(p => p.Availability.Any(a => a.Day == day));
        }

        return await query
            .Include(p => p.Availability)
            .Include(p => p.Services)
            .Include(p => p.Certifications)
            .ThenInclude(c => c.Certification)
            // Deterministic ordering so the candidate ceiling truncates predictably.
            .OrderBy(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<CaregiverProfile?> FindListableByIdAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        ListableCaregivers
            .Include(p => p.Availability)
            .Include(p => p.Services)
            .ThenInclude(s => s.Category)
            .Include(p => p.Certifications)
            .ThenInclude(c => c.Certification)
            .SingleOrDefaultAsync(p => p.Id == caregiverProfileId, cancellationToken);

    /// <summary>
    /// Deliberately tracked, unlike the other reads here.
    ///
    /// A caregiver setting their offered services attaches the result to their profile.
    /// Because the domain assigns its own Guid keys, EF treats a detached entity added
    /// to a tracked navigation as new and tries to INSERT the service row again — which
    /// fails on the primary key. Tracking it tells EF the row already exists, so only
    /// the join row is written.
    /// </summary>
    public Task<CareService?> FindServiceByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();

        return context.CareServices
            .SingleOrDefaultAsync(s => s.Code == normalized && s.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<CareServiceCategory>> ListCategoriesAsync(
        CancellationToken cancellationToken) =>
        await context.CareServiceCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CareService>> ListServicesAsync(CancellationToken cancellationToken) =>
        await context.CareServices
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> ListFavoriteCaregiverIdsAsync(
        Guid careRequesterProfileId,
        CancellationToken cancellationToken)
    {
        var ids = await context.Favorites
            .AsNoTracking()
            .Where(f => f.CareRequesterProfileId == careRequesterProfileId)
            .Select(f => f.CaregiverProfileId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    /// <summary>
    /// Fetches only the name fields. Loading whole User rows here would put email
    /// addresses one careless projection away from a public search response.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, CaregiverName>> ListCaregiverNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, CaregiverName>();
        }

        var names = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);

        return names.ToDictionary(n => n.Id, n => new CaregiverName(n.FirstName, n.LastName));
    }
}

public sealed class FavoriteRepository(GoSitDbContext context) : IFavoriteRepository
{
    public Task<bool> ExistsAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        context.Favorites.AnyAsync(
            f => f.CareRequesterProfileId == careRequesterProfileId
                && f.CaregiverProfileId == caregiverProfileId,
            cancellationToken);

    public Task<int> CountAsync(Guid careRequesterProfileId, CancellationToken cancellationToken) =>
        context.Favorites.CountAsync(
            f => f.CareRequesterProfileId == careRequesterProfileId, cancellationToken);

    public void Add(Guid careRequesterProfileId, Guid caregiverProfileId, DateTime utcNow) =>
        context.Favorites.Add(Favorite.Create(careRequesterProfileId, caregiverProfileId, utcNow));

    public async Task RemoveAsync(
        Guid careRequesterProfileId,
        Guid caregiverProfileId,
        CancellationToken cancellationToken)
    {
        var favorite = await context.Favorites.SingleOrDefaultAsync(
            f => f.CareRequesterProfileId == careRequesterProfileId
                && f.CaregiverProfileId == caregiverProfileId,
            cancellationToken);

        if (favorite is not null)
        {
            context.Favorites.Remove(favorite);
        }
    }

    public async Task<IReadOnlyList<CaregiverProfile>> ListSavedCaregiversAsync(
        Guid careRequesterProfileId,
        CancellationToken cancellationToken) =>
        await context.Favorites
            .AsNoTracking()
            .Where(f => f.CareRequesterProfileId == careRequesterProfileId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Join(
                context.CaregiverProfiles
                    .Include(p => p.Services)
                    .Include(p => p.Certifications)
                    .ThenInclude(c => c.Certification),
                favorite => favorite.CaregiverProfileId,
                caregiver => caregiver.Id,
                (_, caregiver) => caregiver)
            .ToListAsync(cancellationToken);
}
