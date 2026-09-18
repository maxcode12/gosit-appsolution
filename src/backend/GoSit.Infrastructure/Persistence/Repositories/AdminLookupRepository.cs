using GoSit.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

/// <summary>
/// Name and status lookups for the staff queues. See <see cref="IAdminLookupRepository"/>
/// for why this is deliberately separate from the marketplace repositories.
/// </summary>
public sealed class AdminLookupRepository(GoSitDbContext context) : IAdminLookupRepository
{
    public async Task<IReadOnlyDictionary<Guid, AdminUserSummary>> ListUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, AdminUserSummary>();
        }

        var users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Role,
                u.Status,
            })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u => new AdminUserSummary(
                u.Id,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.Email,
                u.Role.ToString(),
                u.Status.ToString()));
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> ListCaregiverOwnersAsync(
        IReadOnlyCollection<Guid> caregiverProfileIds,
        CancellationToken cancellationToken)
    {
        if (caregiverProfileIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var pairs = await context.CaregiverProfiles
            .AsNoTracking()
            .Where(p => caregiverProfileIds.Contains(p.Id))
            .Select(p => new { p.Id, p.UserId })
            .ToListAsync(cancellationToken);

        return pairs.ToDictionary(p => p.Id, p => p.UserId);
    }
}
