using GoSit.Application.Abstractions;
using GoSit.Domain.Growth;
using GoSit.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionRepository(GoSitDbContext context) : ISubscriptionRepository
{
    public async Task<IReadOnlyList<SubscriptionPlan>> ListPlansAsync(
        PlanAudience audience,
        CancellationToken cancellationToken) =>
        await context.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive && p.Audience == audience)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

    public Task<SubscriptionPlan?> FindPlanByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();

        return context.SubscriptionPlans
            .SingleOrDefaultAsync(p => p.Code == normalized && p.IsActive, cancellationToken);
    }

    public Task<SubscriptionPlan?> FindFreePlanAsync(
        PlanAudience audience,
        CancellationToken cancellationToken) =>
        context.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive && p.Audience == audience && p.MonthlyPrice == 0m)
            .OrderBy(p => p.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Returns Active and PastDue rows with the plan loaded. Whether either actually
    /// entitles anything is decided against the clock by the caller — a lapsed row can
    /// still be sitting here between sweeps.
    /// </summary>
    public Task<UserSubscription?> FindActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        context.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId)
            .Where(s => s.Status == SubscriptionStatus.Active
                || s.Status == SubscriptionStatus.PastDue)
            .OrderByDescending(s => s.CurrentPeriodEndUtc)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Any status, unlike <see cref="FindActiveForUserAsync"/> — a cancellation webhook
    /// is an event about a subscription that is ending, so filtering to active rows
    /// would drop exactly the events that need applying.
    /// </summary>
    public Task<UserSubscription?> FindByProviderSubscriptionIdAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken) =>
        context.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(
                s => s.ProviderSubscriptionId == providerSubscriptionId, cancellationToken);

    public void Add(UserSubscription subscription) => context.UserSubscriptions.Add(subscription);

    /// <summary>
    /// One query for a whole page of accounts.
    ///
    /// The status filter runs in SQL; <see cref="UserSubscription.IsActiveAt"/> then runs
    /// in memory, because a past-due grace period is domain logic rather than something
    /// to reimplement in a query and let drift. The candidate set is bounded by the page
    /// being ranked, so the difference is rows already being fetched, not a table scan.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> ListUserIdsWithFeatureAsync(
        IReadOnlyCollection<Guid> userIds,
        string featureCode,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var candidates = await context.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => userIds.Contains(s.UserId))
            .Where(s => s.Status == SubscriptionStatus.Active
                || s.Status == SubscriptionStatus.PastDue)
            .Where(s => s.Plan.Features.Contains(featureCode))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(s => s.IsActiveAt(utcNow))
            .Select(s => s.UserId)
            .ToHashSet();
    }

    public async Task<IReadOnlyList<UserSubscription>> ListDueForExpiryAsync(
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken) =>
        await context.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active
                || s.Status == SubscriptionStatus.PastDue)
            // Past-due rows are pulled in regardless of period end, because their grace
            // runs from the failure rather than from the period.
            .Where(s => s.CurrentPeriodEndUtc <= utcNow || s.Status == SubscriptionStatus.PastDue)
            .OrderBy(s => s.CurrentPeriodEndUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}

public sealed class GrowthRepository(GoSitDbContext context) : IGrowthRepository
{
    public Task<Referral?> FindByRefereeAsync(Guid refereeUserId, CancellationToken cancellationToken) =>
        context.Referrals.SingleOrDefaultAsync(r => r.RefereeUserId == refereeUserId, cancellationToken);

    public async Task<IReadOnlyList<Referral>> ListByReferrerAsync(
        Guid referrerUserId,
        CancellationToken cancellationToken) =>
        await context.Referrals
            .AsNoTracking()
            .Where(r => r.ReferrerUserId == referrerUserId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(Referral referral) => context.Referrals.Add(referral);

    public Task<PromoCode?> FindPromoByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = ReferralCode.Normalize(code);

        return context.PromoCodes.SingleOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    public Task<PromoCode?> FindPromoByIdAsync(
        Guid promoCodeId,
        CancellationToken cancellationToken) =>
        context.PromoCodes.SingleOrDefaultAsync(c => c.Id == promoCodeId, cancellationToken);

    /// <summary>
    /// Newest first, inactive included. Read-only, so no tracking.
    ///
    /// Unpaged on purpose: promo codes are campaigns a person types out one at a time,
    /// not a table that grows on its own. If that ever stops being true this needs a
    /// page, and the honest signal will be a staff member scrolling.
    /// </summary>
    public async Task<IReadOnlyList<PromoCode>> ListPromosAsync(CancellationToken cancellationToken) =>
        await context.PromoCodes
            .AsNoTracking()
            .OrderByDescending(c => c.ValidFromUtc)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> HasRedeemedAsync(
        Guid promoCodeId,
        Guid userId,
        CancellationToken cancellationToken) =>
        context.PromoRedemptions.AnyAsync(
            r => r.PromoCodeId == promoCodeId && r.UserId == userId, cancellationToken);

    public void Add(PromoCode promoCode) => context.PromoCodes.Add(promoCode);

    public void Add(PromoRedemption redemption) => context.PromoRedemptions.Add(redemption);
}
