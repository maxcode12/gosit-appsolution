using GoSit.Application.Abstractions;
using GoSit.Domain.Caregivers;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

/// <summary>
/// The query side of certification review. See <see cref="ICertificationReviewRepository"/>
/// for why this is separate from the aggregate-scoped profile repository.
/// </summary>
public sealed class CertificationReviewRepository(GoSitDbContext context)
    : ICertificationReviewRepository
{
    public async Task<IReadOnlyList<PendingCertificationRow>> ListPendingAsync(
        int take,
        CancellationToken cancellationToken)
    {
        return await context.Set<CaregiverCertification>()
            .AsNoTracking()
            .Where(c => c.Status == CertificationStatus.Pending)
            // Oldest first: someone waiting longest is the one being kept from work.
            .OrderBy(c => c.SubmittedAtUtc)
            .Take(take)
            .Select(c => new PendingCertificationRow(
                c.Id,
                c.CaregiverProfileId,
                c.Certification.Code,
                c.Certification.Name,
                c.IssuedOn,
                c.ExpiresOn,
                c.DocumentReference != null,
                c.SubmittedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListProfileIdsWithLapsingCertificationsAsync(
        DateOnly today,
        int daysAhead,
        int take,
        CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(daysAhead);

        return await context.Set<CaregiverCertification>()
            .AsNoTracking()
            .Where(c =>
                c.Status == CertificationStatus.Verified
                && c.ExpiresOn != null
                && c.ExpiresOn <= horizon
                // Anything already past the Expired notice has nothing left to be told.
                && c.ExpiryNoticeStage != ExpiryNoticeStage.Expired)
            .Select(c => c.CaregiverProfileId)
            .Distinct()
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> FindOwningProfileIdAsync(
        Guid caregiverCertificationId,
        CancellationToken cancellationToken)
    {
        var owner = await context.Set<CaregiverCertification>()
            .AsNoTracking()
            .Where(c => c.Id == caregiverCertificationId)
            .Select(c => (Guid?)c.CaregiverProfileId)
            .SingleOrDefaultAsync(cancellationToken);

        return owner;
    }
}

/// <summary>
/// The query side of insurance review. Mirrors <see cref="CertificationReviewRepository"/>
/// exactly, for the same reason: the aggregate-scoped profile repository is wrong for a
/// queue spanning every caregiver.
/// </summary>
public sealed class InsuranceReviewRepository(GoSitDbContext context) : IInsuranceReviewRepository
{
    public async Task<IReadOnlyList<PendingInsuranceRow>> ListPendingAsync(
        int take,
        CancellationToken cancellationToken) =>
        await context.Set<CaregiverInsurancePolicy>()
            .AsNoTracking()
            .Where(p => p.Status == InsuranceVerificationStatus.Pending)
            .OrderBy(p => p.SubmittedAtUtc)
            .Take(take)
            .Select(p => new PendingInsuranceRow(
                p.Id,
                p.CaregiverProfileId,
                p.CoverageType,
                p.Insurer,
                p.PolicyReference,
                p.LiabilityLimit,
                p.EffectiveOn,
                p.ExpiresOn,
                p.DocumentReference != null,
                p.SubmittedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListProfileIdsWithLapsingInsuranceAsync(
        DateOnly today,
        int daysAhead,
        int take,
        CancellationToken cancellationToken)
    {
        var horizon = today.AddDays(daysAhead);

        return await context.Set<CaregiverInsurancePolicy>()
            .AsNoTracking()
            .Where(p =>
                p.Status == InsuranceVerificationStatus.Verified
                && p.ExpiresOn <= horizon
                && p.ExpiryNoticeStage != ExpiryNoticeStage.Expired)
            .Select(p => p.CaregiverProfileId)
            .Distinct()
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> FindOwningProfileIdAsync(
        Guid caregiverInsurancePolicyId,
        CancellationToken cancellationToken)
    {
        var owner = await context.Set<CaregiverInsurancePolicy>()
            .AsNoTracking()
            .Where(p => p.Id == caregiverInsurancePolicyId)
            .Select(p => (Guid?)p.CaregiverProfileId)
            .SingleOrDefaultAsync(cancellationToken);

        return owner;
    }
}
