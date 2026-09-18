using GoSit.Application.Abstractions;
using GoSit.Domain.CareRequesters;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class CareRequesterProfileRepository(GoSitDbContext context) : ICareRequesterProfileRepository
{
    public Task<CareRequesterProfile?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        context.CareRequesterProfiles
            .Include(p => p.CareRecipients)
            .ThenInclude(r => r.CareNeeds)
            .Include(p => p.EmergencyContacts)
            .Include(p => p.Pets)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.CareRequesterProfiles.AnyAsync(p => p.UserId == userId, cancellationToken);

    /// <summary>
    /// Queried straight off the contacts rather than by loading the profile, so this
    /// cannot become a way to read a stranger's family. See the interface for why that
    /// matters.
    /// </summary>
    public async Task<IReadOnlyList<EmergencyContact>> ListEmergencyContactsAsync(
        Guid careRequesterProfileId,
        CancellationToken cancellationToken) =>
        await context.Set<EmergencyContact>()
            .AsNoTracking()
            .Where(c => c.CareRequesterProfileId == careRequesterProfileId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(CareRequesterProfile profile) => context.CareRequesterProfiles.Add(profile);
}

public sealed class CaregiverProfileRepository(GoSitDbContext context) : ICaregiverProfileRepository
{
    public Task<Domain.Caregivers.CaregiverProfile?> FindByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        context.CaregiverProfiles
            .Include(p => p.Availability)
            .Include(p => p.Certifications)
            .ThenInclude(c => c.Certification)
            // Services decide which checks a caregiver needs: pet care only, or people too.
            .Include(p => p.Services)
            .Include(p => p.InsurancePolicies)
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    /// <summary>
    /// Loads the profile with its certifications, because callers that fetch a profile
    /// by id are acting on the aggregate — staff clearing a certificate, for instance —
    /// and a profile whose collection is empty silently reports the certificate missing.
    ///
    /// Services are loaded for the same reason: the expiry sweeper asks the aggregate
    /// which services a lapsing certificate costs the caregiver, and an unloaded
    /// collection would answer "none" rather than fail.
    /// </summary>
    public Task<Domain.Caregivers.CaregiverProfile?> FindByIdAsync(
        Guid caregiverProfileId,
        CancellationToken cancellationToken) =>
        context.CaregiverProfiles
            .Include(p => p.Certifications)
            .ThenInclude(c => c.Certification)
            .Include(p => p.Services)
            .Include(p => p.InsurancePolicies)
            .SingleOrDefaultAsync(p => p.Id == caregiverProfileId, cancellationToken);

    public Task<Domain.Caregivers.CaregiverProfile?> FindByPayoutAccountIdAsync(
        string payoutAccountId,
        CancellationToken cancellationToken) =>
        context.CaregiverProfiles
            .SingleOrDefaultAsync(p => p.PayoutAccountId == payoutAccountId, cancellationToken);

    public Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        context.CaregiverProfiles.AnyAsync(p => p.UserId == userId, cancellationToken);

    public void Add(Domain.Caregivers.CaregiverProfile profile) => context.CaregiverProfiles.Add(profile);
}

public sealed class CertificationRepository(GoSitDbContext context) : ICertificationRepository
{
    public Task<Domain.Caregivers.Certification?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();

        return context.Certifications
            .SingleOrDefaultAsync(c => c.Code == normalized && c.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<Domain.Caregivers.Certification>> ListActiveAsync(
        CancellationToken cancellationToken) =>
        await context.Certifications
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
}
